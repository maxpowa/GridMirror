using Sandbox.Game.SessionComponents.Clipboard;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Plugins;
using VRageMath;

namespace GridMirror;

public class Plugin : IPlugin
{
    private const string CmdPrefix = "/mirror";
    private bool _hooked;

    public void Init(object gameInstance)
    {
    }

    public void Update()
    {
        if (!_hooked && MyAPIGateway.Utilities != null)
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEntered;
            _hooked = true;
        }
    }

    public void Dispose()
    {
        if (_hooked)
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEntered;
            _hooked = false;
        }
    }

    private void OnMessageEntered(ulong sender, string messageText, ref bool sendToOthers)
    {
        if (!messageText.StartsWith(CmdPrefix, System.StringComparison.OrdinalIgnoreCase))
            return;

        sendToOthers = false;

        if (MyAPIGateway.Session == null)
            return;

        if (!MyAPIGateway.Session.CreativeMode && !MyAPIGateway.Session.HasCreativeRights)
        {
            ShowMessage("Creative tools must be enabled to use mirror commands.");
            return;
        }

        var parts = messageText.Split(' ');

        if (parts.Length > 1 && parts[1].Equals("help", System.StringComparison.OrdinalIgnoreCase))
        {
            ShowHelp();
            return;
        }

        var axis = MirrorAxis.X;

        if (parts.Length > 1)
        {
            switch (parts[1].ToUpperInvariant())
            {
                case "X": axis = MirrorAxis.X; break;
                case "Y": axis = MirrorAxis.Y; break;
                case "Z": axis = MirrorAxis.Z; break;
                default:
                    ShowMessage("Unknown axis: " + parts[1] + ". Use X, Y, or Z.");
                    return;
            }
        }

        ExecuteMirror(axis);
    }

    private static void ExecuteMirror(MirrorAxis axis)
    {
        var grid = GetTargetGrid();
        if (grid == null)
        {
            ShowMessage("No grid found. Look at a grid.");
            return;
        }

        var player = MyAPIGateway.Session?.Player;
        if (player == null)
            return;

        if (!CanCopyGrid(grid, player))
        {
            ShowMessage("You don't have permission to copy this grid.");
            return;
        }

        var result = GridMirror.Mirror(grid, axis);

        if (result.MirroredGrid == null)
        {
            ShowMessage("Mirror failed — grid has no blocks.");
            return;
        }

        var clipComp = MyClipboardComponent.Static;
        if (clipComp?.Clipboard == null)
        {
            ShowMessage("Clipboard unavailable.");
            return;
        }

        var playerPos = player.GetPosition();
        var gridPos = grid.WorldMatrix.Translation;
        var distance = (float)(gridPos - playerPos).Length();

        clipComp.Clipboard.SetGridFromBuilders(
            new MyObjectBuilder_CubeGrid[] { result.MirroredGrid },
            Vector3.Zero,
            distance,
            false);
        clipComp.Clipboard.Activate(null);

        ShowMessage("Axis: " + axis + ". " + result + ". Grid copied to clipboard — paste with Ctrl+V.");
    }

    private static bool CanCopyGrid(IMyCubeGrid grid, IMyPlayer player)
    {
        var playerId = player.IdentityId;

        if (player.PromoteLevel >= MyPromoteLevel.Admin)
            return true;

        var blocks = new System.Collections.Generic.List<IMySlimBlock>();
        grid.GetBlocks(blocks);

        if (blocks.Count == 0)
            return true;

        var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
        int ownedOrShared = 0;

        foreach (var slim in blocks)
        {
            if (slim.FatBlock == null)
            {
                ownedOrShared++;
                continue;
            }

            long ownerId = slim.FatBlock.OwnerId;

            if (ownerId == 0 || ownerId == playerId)
            {
                ownedOrShared++;
                continue;
            }

            if (playerFaction != null)
            {
                var ownerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
                if (ownerFaction != null && playerFaction.FactionId == ownerFaction.FactionId)
                {
                    ownedOrShared++;
                    continue;
                }
            }

            var relation = slim.FatBlock.GetUserRelationToOwner(playerId);
            if (relation != VRage.Game.MyRelationsBetweenPlayerAndBlock.Enemies)
            {
                ownedOrShared++;
            }
        }

        return ownedOrShared > blocks.Count / 2;
    }

    private static IMyCubeGrid GetTargetGrid()
    {
        var camera = MyAPIGateway.Session?.Camera;
        if (camera != null)
        {
            var from = camera.WorldMatrix.Translation;
            var to = from + camera.WorldMatrix.Forward * 200;

            IHitInfo hit;
            if (MyAPIGateway.Physics.CastRay(from, to, out hit))
            {
                if (hit.HitEntity is IMyCubeGrid hitGrid)
                    return hitGrid;
            }
        }

        return null!;
    }

    private static void ShowMessage(string text)
    {
        MyAPIGateway.Utilities.ShowMessage("Mirror", text);
    }

    private static void ShowHelp()
    {
        ShowMessage("Usage: /mirror [X|Y|Z]");
        ShowMessage("  X = left/right, Y = up/down, Z = forward/back");
        ShowMessage("  Look at the target grid.");
        ShowMessage("  Mirrored grid is copied to clipboard for pasting.");
    }
}
