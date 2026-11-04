// IDirectionalReceiver.cs
public interface IDirectionalReceiver
{
    // dir: 0..7 for octants, -1 = idle
    void OnDirectionChanged(int dir);

    // Called when joystick is idle
    void OnIdle();
}
