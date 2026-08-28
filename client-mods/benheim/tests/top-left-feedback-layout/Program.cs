using System;
using BenheimQoL;

RunCase(
    "1280x720 scale 1",
    screenWidth: 1280f,
    screenHeight: 720f,
    scaleFactor: 1f,
    laneWidth: 300f,
    laneHeight: 48f,
    gap: 8f,
    margin: 16f,
    hotbar: new TopLeftFeedbackRect(320f, 460f, 640f, 80f),
    nativeStatus: null);

RunCase(
    "1920x1080 scale 1.25 with native collision",
    screenWidth: 1920f,
    screenHeight: 1080f,
    scaleFactor: 1.25f,
    laneWidth: 400f,
    laneHeight: 190f,
    gap: 10f,
    margin: 20f,
    hotbar: new TopLeftFeedbackRect(240f, 640f, 960f, 90f),
    nativeStatus: new TopLeftFeedbackRect(0f, 450f, 900f, 350f));

RunCase(
    "2560x1440 scale 2",
    screenWidth: 2560f,
    screenHeight: 1440f,
    scaleFactor: 2f,
    laneWidth: 640f,
    laneHeight: 96f,
    gap: 16f,
    margin: 32f,
    hotbar: new TopLeftFeedbackRect(720f, 900f, 1120f, 110f),
    nativeStatus: null);

RunFallbackCase(
    "1280x720 scale 1.5 fallback",
    screenWidth: 1280f,
    screenHeight: 720f,
    scaleFactor: 1.5f,
    laneWidth: 400f,
    laneHeight: 72f,
    gap: 12f,
    margin: 24f);

RunWideLaneCase();

Console.WriteLine("top-left feedback layout geometry checks passed");
return;

static void RunCase(
    string name,
    float screenWidth,
    float screenHeight,
    float scaleFactor,
    float laneWidth,
    float laneHeight,
    float gap,
    float margin,
    TopLeftFeedbackRect hotbar,
    TopLeftFeedbackRect? nativeStatus)
{
    TopLeftFeedbackPlacement placement = TopLeftFeedbackLayout.Calculate(
        screenWidth,
        screenHeight,
        scaleFactor,
        laneWidth,
        laneHeight,
        gap,
        margin,
        hasHotbar: true,
        hotbar,
        hasNativeStatus: nativeStatus.HasValue,
        nativeStatus ?? default,
        fallbackLeft: 24f,
        fallbackTopOffset: 120f);

    ExpectTrue(placement.UsesHotbar, name + ": uses live hotbar");
    ExpectClose(hotbar.XMin, placement.X, name + ": left aligns with hotbar");
    ExpectTrue(
        placement.TopY <= hotbar.YMin - gap + 0.001f,
        name + ": stays beneath hotbar");
    ExpectWithinScreen(placement, screenWidth, screenHeight, margin, name);
    if (nativeStatus.HasValue)
    {
        ExpectTrue(
            placement.TopY <= nativeStatus.Value.YMin - gap + 0.001f,
            name + ": resolves native status vertically");
        ExpectTrue(
            !placement.Bounds.Overlaps(nativeStatus.Value),
            name + ": does not overlap native status");
    }
}

static void RunFallbackCase(
    string name,
    float screenWidth,
    float screenHeight,
    float scaleFactor,
    float laneWidth,
    float laneHeight,
    float gap,
    float margin)
{
    TopLeftFeedbackPlacement placement = TopLeftFeedbackLayout.Calculate(
        screenWidth,
        screenHeight,
        scaleFactor,
        laneWidth,
        laneHeight,
        gap,
        margin,
        hasHotbar: false,
        default,
        hasNativeStatus: false,
        default,
        fallbackLeft: 24f,
        fallbackTopOffset: 120f);

    ExpectTrue(!placement.UsesHotbar, name + ": uses fallback");
    ExpectClose(
        MathF.Max(margin, 24f * scaleFactor),
        placement.X,
        name + ": fallback keeps left anchor");
    ExpectWithinScreen(placement, screenWidth, screenHeight, margin, name);
    ExpectTrue(
        placement.TopY <= screenHeight - 120f * scaleFactor + 0.001f,
        name + ": fallback uses top offset from screen top");
}

static void RunWideLaneCase()
{
    const float screenWidth = 1280f;
    const float margin = 16f;
    TopLeftFeedbackRect hotbar = new TopLeftFeedbackRect(200f, 540f, 700f, 80f);
    TopLeftFeedbackPlacement placement = TopLeftFeedbackLayout.Calculate(
        screenWidth,
        screenHeight: 720f,
        scaleFactor: 1f,
        laneWidth: 1200f,
        laneHeight: 80f,
        gap: 8f,
        margin,
        hasHotbar: true,
        hotbar,
        hasNativeStatus: false,
        default,
        fallbackLeft: 24f,
        fallbackTopOffset: 120f);

    ExpectTrue(placement.X <= hotbar.XMin, "wide lane: never moves right of hotbar");
    ExpectTrue(
        placement.Bounds.XMax <= screenWidth - margin + 0.001f,
        "wide lane: right edge remains on screen");
    ExpectTrue(
        placement.TopY <= hotbar.YMin - 8f + 0.001f,
        "wide lane: remains beneath hotbar");
}

static void ExpectWithinScreen(
    TopLeftFeedbackPlacement placement,
    float screenWidth,
    float screenHeight,
    float margin,
    string name)
{
    ExpectTrue(placement.Bounds.XMin >= margin - 0.001f, name + ": left on screen");
    ExpectTrue(placement.Bounds.XMax <= screenWidth - margin + 0.001f, name + ": right on screen");
    ExpectTrue(placement.Bounds.YMin >= margin - 0.001f, name + ": bottom on screen");
    ExpectTrue(placement.Bounds.YMax <= screenHeight - margin + 0.001f, name + ": top on screen");
}

static void ExpectClose(float expected, float actual, string name)
{
    if (MathF.Abs(expected - actual) > 0.001f)
    {
        throw new InvalidOperationException(
            $"{name}: expected {expected}, got {actual}");
    }
}

static void ExpectTrue(bool value, string name)
{
    if (!value)
    {
        throw new InvalidOperationException(name);
    }
}
