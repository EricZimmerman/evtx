using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace evtx.Test;

/// <summary>
///     A folder the current user can't list or read (a Deny ACE for read and execute), until disposed. Windows only
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DeniedFolder : IDisposable
{
    private readonly DirectoryInfo _info;
    private readonly FileSystemAccessRule _rule;

    public DeniedFolder(string path)
    {
        _info = Directory.CreateDirectory(path);
        _rule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny);
        var security = _info.GetAccessControl();
        security.AddAccessRule(_rule);
        _info.SetAccessControl(security);
    }

    public string Path => _info.FullName;

    public void Dispose()
    {
        var security = _info.GetAccessControl();
        security.RemoveAccessRule(_rule);
        _info.SetAccessControl(security);
    }
}
