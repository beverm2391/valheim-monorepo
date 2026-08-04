# Shortcuts

The Shortcuts module gives players one Valheim-styled Benheim menu built with
Unity UI and Valheim's loaded UI templates for discovering controls, passive
features, and multiplayer compatibility.

## In Development

- `Left Shift + B` shows or hides the menu unless the player is typing in a
  text field or console. This menu has not passed gameplay proof.
- The title shows the loaded Benheim version. The menu lists active controls
  and passive features without changing gameplay.
- Show the Benheim Inventory server version and transaction protocol.
- Show each ready player's Benheim version, protocol, detection state, and
  Put Away compatibility.
- Update the version roster when ready players join or leave, or when reported
  compatibility changes.
- Use the transaction protocol version to decide compatibility. Show exact
  semantic versions only for diagnosis.
- Preload the menu before the first `Left Shift + B` press.
- Group features under six color-coded headings: Inventory, Build & Repair,
  Farming, Travel, Combat & Skills, and Help.
- Explain which items Put Away always protects and which nearby chests can
  receive items.
- Explain that a gold `P` means manually pocketed. Equipped and hotbar items
  remain protected without showing a marker.
- Explain that manual pocketing protects every stack of a stackable item type
  and only the marked non-stackable item.
- List `Left Shift` + interact for filling production inputs, cooking slots,
  and fuel.
- List Wood Cutting cleave as a passive skill feature.
- `F7` copies the active Benheim diagnostic log to the player's Desktop with a
  timestamped filename on Mac and Windows.
- Confirm the exported filename in game so the player can attach it when
  reporting a problem.
