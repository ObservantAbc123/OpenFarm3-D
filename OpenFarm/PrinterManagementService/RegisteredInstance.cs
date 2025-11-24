using OctoprintHelper;

namespace PrintManagement;

public class RegisteredInstance(
    int pid,
    string handle,
    HttpClient clientConnection,
    bool connected,
    MinimalStateReport? state = null,
    long activePrintId = -1)
{
    public int _pid { get; set; } = pid; // printer unique id
    public string _handle { get; set; } = handle;
    public HttpClient _clientConnection { get; set; } = clientConnection;
    public MinimalStateReport? _state { get; set; } = state;
    public bool _connected { get; set; } = connected;
    public long _activePrintId { get; set; } = activePrintId;
}

