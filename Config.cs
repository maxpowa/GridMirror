using GridMirror.Settings;
using GridMirror.Settings.Elements;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VRage.Input;

using Binding = GridMirror.Settings.Tools.Binding;

namespace GridMirror;

public class Config : INotifyPropertyChanged
{
    #region Options

    private Binding mirrorX = new Binding(MyKeys.X, ctrl: true, shift: true);
    private Binding mirrorY = new Binding(MyKeys.None);
    private Binding mirrorZ = new Binding(MyKeys.None);

    #endregion

    #region User interface

    public readonly string Title = "Grid Mirror";

    [Separator("Mirror Hotkeys")]

    [Keybind(description: "Mirror clipboard grid along X axis (left/right)")]
    public Binding MirrorX
    {
        get => mirrorX;
        set => SetField(ref mirrorX, value);
    }

    [Keybind(description: "Mirror clipboard grid along Y axis (up/down)")]
    public Binding MirrorY
    {
        get => mirrorY;
        set => SetField(ref mirrorY, value);
    }

    [Keybind(description: "Mirror clipboard grid along Z axis (forward/back)")]
    public Binding MirrorZ
    {
        get => mirrorZ;
        set => SetField(ref mirrorZ, value);
    }

    #endregion

    #region Property change notification boilerplate

    public static readonly Config Default = new Config();
    public static readonly Config Current = ConfigStorage.Load();

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
