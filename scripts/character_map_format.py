"""Narrow, read-only parser for the map data embedded in a Valheim character."""

from __future__ import annotations

import gzip
import hashlib
import io
import math
import struct
from dataclasses import dataclass
from pathlib import Path
from typing import Any


SUPPORTED_CHARACTER_VERSION = 43
SUPPORTED_MAP_VERSION = 8
SUPPORTED_MAP_TEXTURE_SIZE = 2048
DEFAULT_PIXEL_SIZE_METERS = 12.0
DEFAULT_WORLD_SIZE_METERS = 10_000.0
MAX_CHARACTER_BYTES = 128 * 1024 * 1024
MAX_MAP_PAYLOAD_BYTES = 64 * 1024 * 1024
MAX_STRING_BYTES = 1024 * 1024
MAX_COLLECTION_ITEMS = 1_000_000

PIN_TYPES = (
    "Icon0",
    "Icon1",
    "Icon2",
    "Icon3",
    "Death",
    "Bed",
    "Icon4",
    "Shout",
    "None",
    "Boss",
    "Player",
    "RandomEvent",
    "Ping",
    "EventArea",
    "Hildir1",
    "Hildir2",
    "Hildir3",
)


class InspectionError(Exception):
    """The input cannot be inspected safely with the supported format."""


class Reader:
    def __init__(self, data: bytes, label: str) -> None:
        self.data = data
        self.label = label
        self.position = 0

    @property
    def remaining(self) -> int:
        return len(self.data) - self.position

    def take(self, count: int) -> bytes:
        if count < 0 or count > self.remaining:
            raise InspectionError(
                f"{self.label}: truncated at byte {self.position}; "
                f"needed {count} bytes, found {self.remaining}"
            )
        value = self.data[self.position : self.position + count]
        self.position += count
        return value

    def unpack(self, format_: str) -> Any:
        size = struct.calcsize(format_)
        return struct.unpack(format_, self.take(size))[0]

    def uint8(self) -> int:
        return self.unpack("<B")

    def int32(self) -> int:
        return self.unpack("<i")

    def uint32(self) -> int:
        return self.unpack("<I")

    def int64(self) -> int:
        return self.unpack("<q")

    def float32(self) -> float:
        return self.unpack("<f")

    def boolean(self) -> bool:
        offset = self.position
        value = self.uint8()
        if value not in (0, 1):
            raise InspectionError(f"{self.label}: invalid boolean {value} at byte {offset}")
        return bool(value)

    def vector3(self) -> tuple[float, float, float]:
        offset = self.position
        value = self.float32(), self.float32(), self.float32()
        if not all(math.isfinite(component) for component in value):
            raise InspectionError(
                f"{self.label}: non-finite vector component at byte {offset}"
            )
        return value

    def byte_array(self) -> bytes:
        count = self.int32()
        if count < 0:
            raise InspectionError(f"{self.label}: negative byte-array length {count}")
        return self.take(count)

    def string(self) -> str:
        length = 0
        shift = 0
        while shift < 35:
            byte = self.uint8()
            length |= (byte & 0x7F) << shift
            if not byte & 0x80:
                if length > MAX_STRING_BYTES:
                    raise InspectionError(
                        f"{self.label}: string length {length} exceeds the inspection limit"
                    )
                try:
                    return self.take(length).decode("utf-8")
                except UnicodeDecodeError as error:
                    raise InspectionError(f"{self.label}: invalid UTF-8 string") from error
            shift += 7
        raise InspectionError(f"{self.label}: invalid 7-bit string length")

    def require_end(self) -> None:
        if self.remaining:
            raise InspectionError(f"{self.label}: {self.remaining} unread bytes remain")


@dataclass
class Extent:
    pixels: int = 0
    max_radius_meters: float = 0.0

    def add(self, x_meters: float, z_meters: float) -> None:
        self.pixels += 1
        self.max_radius_meters = max(
            self.max_radius_meters, math.hypot(x_meters, z_meters)
        )

    def as_dict(self, world_size_meters: float) -> dict[str, Any]:
        return {
            "pixels": self.pixels,
            "maxRadiusMeters": self.max_radius_meters,
            "worldRadiusPercent": percentage(self.max_radius_meters, world_size_meters),
        }


def percentage(distance_meters: float, world_size_meters: float) -> float:
    return distance_meters / world_size_meters * 100.0


def radial_point(
    position: tuple[float, float, float],
    present: bool,
    world_size_meters: float,
) -> dict[str, Any]:
    radius = math.hypot(position[0], position[2])
    return {
        "present": present,
        "radiusMeters": radius,
        "worldRadiusPercent": percentage(radius, world_size_meters),
    }


def read_collection_count(reader: Reader, label: str) -> int:
    count = reader.int32()
    if count < 0 or count > MAX_COLLECTION_ITEMS:
        raise InspectionError(f"{reader.label}: invalid {label} count {count}")
    return count


def skip_string_float_dictionary(reader: Reader, label: str) -> None:
    count = read_collection_count(reader, label)
    for _ in range(count):
        reader.string()
        reader.float32()


def decompress_map(data: bytes) -> bytes:
    try:
        with gzip.GzipFile(fileobj=io.BytesIO(data)) as archive:
            payload = archive.read(MAX_MAP_PAYLOAD_BYTES + 1)
    except (EOFError, OSError) as error:
        raise InspectionError(f"map: invalid gzip payload: {error}") from error
    if len(payload) > MAX_MAP_PAYLOAD_BYTES:
        raise InspectionError("map: decompressed payload exceeds the inspection limit")
    return payload


def parse_map(
    data: bytes,
    pixel_size_meters: float,
    world_size_meters: float,
) -> dict[str, Any]:
    outer = Reader(data, "map")
    version = outer.int32()
    if version != SUPPORTED_MAP_VERSION:
        raise InspectionError(
            f"map: unsupported version {version}; supported version is {SUPPORTED_MAP_VERSION}"
        )
    compressed = outer.byte_array()
    outer.require_end()

    inner = Reader(decompress_map(compressed), "map payload")
    texture_size = inner.int32()
    if texture_size != SUPPORTED_MAP_TEXTURE_SIZE:
        raise InspectionError(
            f"map: unsupported texture size {texture_size}; "
            f"supported size is {SUPPORTED_MAP_TEXTURE_SIZE}"
        )

    pixel_count = texture_size * texture_size
    personal = inner.take(pixel_count)
    shared = inner.take(pixel_count)
    if any(value not in (0, 1) for value in personal):
        raise InspectionError("map: personal exploration contains an invalid boolean byte")
    if any(value not in (0, 1) for value in shared):
        raise InspectionError("map: shared exploration contains an invalid boolean byte")

    center = texture_size // 2
    extents = {
        "personal": Extent(),
        "sharedAll": Extent(),
        "sharedOnly": Extent(),
        "visibleUnion": Extent(),
    }
    for index, (personal_value, shared_value) in enumerate(zip(personal, shared)):
        if not personal_value and not shared_value:
            continue
        x_meters = (index % texture_size - center) * pixel_size_meters
        z_meters = (index // texture_size - center) * pixel_size_meters
        if personal_value:
            extents["personal"].add(x_meters, z_meters)
        if shared_value:
            extents["sharedAll"].add(x_meters, z_meters)
        if shared_value and not personal_value:
            extents["sharedOnly"].add(x_meters, z_meters)
        extents["visibleUnion"].add(x_meters, z_meters)

    pin_count = read_collection_count(inner, "pin")
    pins = []
    for _ in range(pin_count):
        name = inner.string()
        position = inner.vector3()
        pin_type_value = inner.int32()
        checked = inner.boolean()
        owner_id = inner.int64()
        inner.string()  # author
        pixel_x = round(position[0] / pixel_size_meters + center)
        pixel_z = round(position[2] / pixel_size_meters + center)
        in_map = 0 <= pixel_x < texture_size and 0 <= pixel_z < texture_size
        pixel_index = pixel_z * texture_size + pixel_x if in_map else -1
        radius = math.hypot(position[0], position[2])
        pins.append(
            {
                "name": name,
                "type": (
                    PIN_TYPES[pin_type_value]
                    if 0 <= pin_type_value < len(PIN_TYPES)
                    else f"Unknown({pin_type_value})"
                ),
                "checked": checked,
                "source": "shared-player" if owner_id else "local-or-game",
                "radiusMeters": radius,
                "worldRadiusPercent": percentage(radius, world_size_meters),
                "personallyExploredAtPin": bool(personal[pixel_index]) if in_map else False,
                "sharedExploredAtPin": bool(shared[pixel_index]) if in_map else False,
            }
        )

    inner.boolean()  # public reference position
    inner.require_end()
    return {
        "version": version,
        "textureSize": texture_size,
        "pixelSizeMeters": pixel_size_meters,
        "exploration": {
            name: extent.as_dict(world_size_meters) for name, extent in extents.items()
        },
        "savedPinCount": pin_count,
        "pins": pins,
    }


def read_character_file(path: Path) -> bytes:
    try:
        size = path.stat().st_size
    except OSError as error:
        raise InspectionError(
            f"character: cannot stat input: {error.strerror or error.__class__.__name__}"
        ) from error
    if size > MAX_CHARACTER_BYTES:
        raise InspectionError(f"character: file size {size} exceeds the inspection limit")
    try:
        return path.read_bytes()
    except OSError as error:
        raise InspectionError(
            f"character: cannot read input: {error.strerror or error.__class__.__name__}"
        ) from error


def parse_character(
    path: Path,
    pixel_size_meters: float,
    world_size_meters: float,
) -> tuple[dict[str, Any], list[int]]:
    data = read_character_file(path)
    if len(data) < 76:
        raise InspectionError(f"character: file is too short ({len(data)} bytes)")

    file_length = struct.unpack_from("<I", data)[0]
    payload_end = 4 + file_length
    if payload_end + 68 != len(data):
        raise InspectionError(
            f"character: length header {file_length} does not match file size {len(data)}"
        )
    payload = data[4:payload_end]
    trailer = Reader(data[payload_end:], "character trailer")
    hash_length = trailer.uint32()
    if hash_length != hashlib.sha512().digest_size:
        raise InspectionError(f"character: unsupported trailer hash length {hash_length}")
    expected_hash = trailer.take(hash_length)
    trailer.require_end()
    if hashlib.sha512(payload).digest() != expected_hash:
        raise InspectionError("character: trailer hash does not match the payload")

    reader = Reader(payload, "character payload")
    version = reader.uint32()
    if version != SUPPORTED_CHARACTER_VERSION:
        raise InspectionError(
            f"character: unsupported version {version}; "
            f"supported version is {SUPPORTED_CHARACTER_VERSION}"
        )
    player_stat_count = read_collection_count(reader, "player stat")
    reader.take(player_stat_count * 4)
    reader.boolean()  # first spawn
    world_count = read_collection_count(reader, "world")
    worlds = []
    world_uids = []
    for world_index in range(world_count):
        world_uid = reader.int64()
        world_uids.append(world_uid)
        custom_spawn_present = reader.boolean()
        custom_spawn = reader.vector3()
        logout_present = reader.boolean()
        logout = reader.vector3()
        death_present = reader.boolean()
        death = reader.vector3()
        home = reader.vector3()
        map_present = reader.boolean()
        map_summary = (
            parse_map(reader.byte_array(), pixel_size_meters, world_size_meters)
            if map_present
            else None
        )
        worlds.append(
            {
                "index": world_index + 1,
                "label": f"world-{world_index + 1}",
                "customSpawn": radial_point(
                    custom_spawn, custom_spawn_present, world_size_meters
                ),
                "logout": radial_point(logout, logout_present, world_size_meters),
                "death": radial_point(death, death_present, world_size_meters),
                "home": radial_point(home, True, world_size_meters),
                "map": map_summary,
            }
        )

    # Consume the outer format to detect drift, but do not retain unrelated
    # character fields in the report.
    reader.string()  # player name
    reader.int64()  # player ID
    reader.string()  # start seed
    reader.boolean()  # used cheats
    reader.int64()  # creation time
    skip_string_float_dictionary(reader, "known world")
    skip_string_float_dictionary(reader, "known world key")
    skip_string_float_dictionary(reader, "known command")
    skip_string_float_dictionary(reader, "enemy stat")
    skip_string_float_dictionary(reader, "item pickup stat")
    skip_string_float_dictionary(reader, "item craft stat")
    if reader.boolean():
        reader.byte_array()
    reader.require_end()

    return (
        {
            "characterVersion": version,
            "worldCount": world_count,
            "calibration": {
                "pixelSizeMeters": pixel_size_meters,
                "worldRadiusMeters": world_size_meters,
            },
            "worlds": worlds,
        },
        world_uids,
    )
