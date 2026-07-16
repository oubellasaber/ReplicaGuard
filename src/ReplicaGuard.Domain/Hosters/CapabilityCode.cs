namespace ReplicaGuard.Domain.Hosters;

public enum CapabilityCode : short
{
    RemoteFileUpload = 1,
    LocalFileUpload = 2,
    IdentityVerification = 3,
    CopyFile = 4,
    GenerateDownloadUrl = 5,
    GetFileInfo = 6,
    GetLastDownloadDate = 7
}
