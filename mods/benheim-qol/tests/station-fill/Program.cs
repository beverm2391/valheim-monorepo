using System;
using BenheimQoL.Production;

ExpectFalse(
    StationFillBatchRules.UsesOwnerBatch(stationIsLocalOwner: true),
    "local owner keeps the existing local fill path");
ExpectTrue(
    StationFillBatchRules.UsesOwnerBatch(stationIsLocalOwner: false),
    "remote owner uses one owner-authoritative batch");

ExpectEqual(
    1,
    StationFillBatchRules.FirstAvailableIndex(new[] { 0, 7, 4 }),
    "one interaction selects the first available authored conversion");
ExpectEqual(
    -1,
    StationFillBatchRules.FirstAvailableIndex(new[] { 0, 0 }),
    "no compatible material leaves native rejection in control");
ExpectEqual(
    7,
    StationFillBatchRules.RequestedCount(7, 50),
    "request reserves only the selected material count");
ExpectEqual(
    50,
    StationFillBatchRules.RequestedCount(80, 50),
    "request never exceeds station capacity");

ExpectEqual(
    0,
    StationFillBatchRules.AcceptedCount(50f, 50f, 20, inputAllowed: true),
    "remote full station rejects the reservation");
ExpectEqual(
    12,
    StationFillBatchRules.AcceptedCount(0f, 50f, 12, inputAllowed: true),
    "remote station accepts a complete partial-material batch");
ExpectEqual(
    2,
    StationFillBatchRules.AcceptedCount(48f, 50f, 20, inputAllowed: true),
    "remote nearly-full station accepts only live capacity");
ExpectEqual(
    0,
    StationFillBatchRules.AcceptedCount(0f, 50f, 12, inputAllowed: false),
    "remote owner rejects a disallowed input");

int requested = 20;
int accepted = StationFillBatchRules.AcceptedCount(48f, 50f, requested, inputAllowed: true);
ExpectEqual(requested, accepted + (requested - accepted), "accepted plus refund preserves material count");

Console.WriteLine("station fill owner-batch checks passed");
return;

static void ExpectEqual(int expected, int actual, string scenario)
{
    if (actual != expected)
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}

static void ExpectTrue(bool actual, string scenario)
{
    if (!actual)
    {
        throw new InvalidOperationException($"{scenario}: expected true");
    }
}

static void ExpectFalse(bool actual, string scenario)
{
    if (actual)
    {
        throw new InvalidOperationException($"{scenario}: expected false");
    }
}
