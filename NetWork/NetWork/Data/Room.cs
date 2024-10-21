
public class Room
{
    private string _roomName;
    private int _roomId;
    private ushort[] _playerId;
    private int _maxCount;
    private int _curCount;
    public Room(string name, int id, int maxCount)
    {
        _roomName = name;
        _roomId = id;
        _playerId = new ushort[maxCount];
        _maxCount = maxCount;
        _curCount = 0;
    }
    
    public bool JoinRoom(ushort id)
    {
        if (_curCount<_maxCount)
        {
            _playerId[_curCount] = id;
            _curCount += 1;
            return true;
        }
        return false;
    }
}
