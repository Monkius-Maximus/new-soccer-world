using Godot;
using SoccerSim.Core.Match;

namespace SoccerDreamGame;

/// <summary>
/// Draws one <see cref="MatchFrame"/>. It knows how to put metres on screen and nothing else —
/// not how a match is run, not how fast, not what happens next. The simulation is the authority
/// on football; this is the consumer ADR-0002 describes.
/// </summary>
public partial class PitchView : Control
{
    private static readonly Color Grass = new(0.16f, 0.42f, 0.22f);
    private static readonly Color Markings = new(1f, 1f, 1f, 0.55f);
    private static readonly Color BallColour = new(1f, 0.98f, 0.9f);

    // Coloured by side, not by club. Real kits come from KitDefinition once the Club editor
    // exists, and inventing club colours here would be a second source of truth for them.
    private static readonly Color HomeOutfield = new(0.29f, 0.56f, 0.93f);
    private static readonly Color HomeKeeper = new(0.65f, 0.80f, 1f);
    private static readonly Color AwayOutfield = new(0.90f, 0.32f, 0.29f);
    private static readonly Color AwayKeeper = new(1f, 0.70f, 0.62f);

    private MatchFrame? _frame;

    /// <summary>
    /// Hands the view a new frame to draw. Named <c>ShowFrame</c> rather than <c>Show</c>
    /// because <c>CanvasItem.Show</c> already exists and means something else.
    /// </summary>
    public void ShowFrame(MatchFrame frame)
    {
        _frame = frame;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var available = Size;
        if (available.X <= 0f || available.Y <= 0f)
        {
            return;
        }

        // Fit the pitch, preserving its real proportions. A stretched pitch would make distances
        // lie, and distance is what the simulation decides everything from.
        var scale = Mathf.Min(available.X / (float)Pitch.Length, available.Y / (float)Pitch.Width);
        var extent = new Vector2((float)Pitch.Length * scale, (float)Pitch.Width * scale);
        var origin = (available - extent) / 2f;

        DrawRect(new Rect2(origin, extent), Grass);

        var halfway = (float)(Pitch.Length / 2.0);
        DrawLine(
            origin + new Vector2(halfway * scale, 0f),
            origin + new Vector2(halfway * scale, extent.Y),
            Markings,
            Mathf.Max(1f, scale * 0.12f));

        var goalWidth = Mathf.Max(2f, scale * 0.3f);
        var goalTop = (float)Pitch.GoalMinY * scale;
        var goalBottom = (float)Pitch.GoalMaxY * scale;
        DrawLine(origin + new Vector2(0f, goalTop), origin + new Vector2(0f, goalBottom), Colors.White, goalWidth);
        DrawLine(
            origin + new Vector2(extent.X, goalTop),
            origin + new Vector2(extent.X, goalBottom),
            Colors.White,
            goalWidth);

        if (_frame is not { } frame)
        {
            return;
        }

        var playerRadius = Mathf.Max(2f, scale * 0.9f);
        foreach (var player in frame.Players)
        {
            var keeper = player.Slot == 0;
            var colour = player.ClubId == frame.HomeClubId
                ? (keeper ? HomeKeeper : HomeOutfield)
                : (keeper ? AwayKeeper : AwayOutfield);

            DrawCircle(origin + Metres(player.Location, scale), playerRadius, colour);
        }

        DrawCircle(origin + Metres(frame.Ball, scale), Mathf.Max(1.5f, scale * 0.45f), BallColour);
    }

    private static Vector2 Metres(Vec2 point, float scale) =>
        new((float)point.X * scale, (float)point.Y * scale);
}
