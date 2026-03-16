using System.Linq;
using System.Reflection;
using Sandbox.Game.SessionComponents.Clipboard;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using GridMirror.Settings;
using GridMirror.Settings.Layouts;
using VRage.Game;
using VRage.Input;
using VRage.Plugins;
using VRageMath;

namespace GridMirror;

public class Plugin : IPlugin
{
    public const string Name = "GridMirror";
    public static Plugin Instance { get; private set; }
    private SettingsGenerator settingsGenerator;

    public void Init(object gameInstance)
    {
        Instance = this;
        Instance.settingsGenerator = new SettingsGenerator();
    }

    public void Update()
    {
        var input = MyInput.Static;
        if (input == null)
            return;

        var clipComp = MyClipboardComponent.Static;
        if (clipComp?.Clipboard == null || !clipComp.Clipboard.IsActive)
            return;

        var config = Config.Current;

        if (config.MirrorX.HasPressed(input))
            ExecuteMirror(MirrorAxis.X);
        else if (config.MirrorY.HasPressed(input))
            ExecuteMirror(MirrorAxis.Y);
        else if (config.MirrorZ.HasPressed(input))
            ExecuteMirror(MirrorAxis.Z);
    }

    public void Dispose()
    {
        Instance = null;
    }

    public void OpenConfigDialog()
    {
        Instance.settingsGenerator.SetLayout<Simple>();
        MyGuiSandbox.AddScreen(Instance.settingsGenerator.Dialog);
    }

    private static void ExecuteMirror(MirrorAxis axis)
    {
        if (MyAPIGateway.Session == null)
            return;

        var clipComp = MyClipboardComponent.Static;
        if (!clipComp.Clipboard.HasCopiedGrids())
        {
            ShowMessage("No grid on clipboard. Copy a grid first (Ctrl+C).");
            return;
        }

        var copiedGrids = clipComp.Clipboard.CopiedGrids;
        var mirroredGrids = new MyObjectBuilder_CubeGrid[copiedGrids.Count];
        int totalBlocks = 0;

        for (int i = 0; i < copiedGrids.Count; i++)
        {
            var gridBuilder = (MyObjectBuilder_CubeGrid)copiedGrids[i].Clone();
            var result = GridMirror.Mirror(gridBuilder, axis);
            if (result.MirroredGrid == null)
            {
                ShowMessage("Mirror failed — grid has no blocks.");
                return;
            }
            mirroredGrids[i] = result.MirroredGrid;
            totalBlocks += result.BlocksMirrored;
        }

        var clipType = clipComp.Clipboard.GetType();
        var dragLength = (float)(clipType.GetField("m_dragDistance", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(clipComp.Clipboard) ?? 0f);

        var firstGrid = mirroredGrids[0];
        var firstBlock = firstGrid.CubeBlocks.FirstOrDefault();
        var gridSize = firstGrid.GridSizeEnum == MyCubeSize.Large ? 2.5f : 0.5f;
        var dragDelta = firstBlock != null ? -new Vector3(firstBlock.Min) * gridSize : Vector3.Zero;

        clipComp.Clipboard.SetGridFromBuilders(
            mirroredGrids,
            dragDelta,
            dragLength,
            true);
        clipComp.Clipboard.Activate(null);

        ShowMessage("Axis: " + axis + ". Blocks mirrored: " + totalBlocks);
    }

    private static void ShowMessage(string text)
    {
        MyAPIGateway.Utilities?.ShowMessage(Name, text);
    }
}
