using Steamworks;

namespace SteamIntegration;

public sealed class SteamManager : IDisposable
{
    private bool disposed;

    public bool IsInitialized { get; private set; }

    public bool IsAvailable => IsInitialized;

    public string? LastError { get; private set; }

    public bool IsLoggedOn
    {
        get
        {
            if (!IsInitialized)
            {
                return false;
            }

            try
            {
                return SteamUser.BLoggedOn();
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }
    }

    public ulong SteamId
    {
        get
        {
            if (!IsInitialized)
            {
                return 0;
            }

            try
            {
                return SteamUser.GetSteamID().m_SteamID;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return 0;
            }
        }
    }

    public uint AppId
    {
        get
        {
            if (!IsInitialized)
            {
                return 0;
            }

            try
            {
                return SteamUtils.GetAppID().m_AppId;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return 0;
            }
        }
    }

    public bool Initialize(uint appId = 0)
    {
        if (IsInitialized)
        {
            return true;
        }

        try
        {
            if (appId > 0 && SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
            {
                LastError = "Steam restart requested.";
                return false;
            }

            IsInitialized = SteamAPI.Init();
            LastError = IsInitialized ? null : "SteamAPI.Init returned false.";
            return IsInitialized;
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            IsInitialized = false;
            return false;
        }
    }

    public void RunCallbacks()
    {
        if (!IsInitialized)
        {
            return;
        }

        try
        {
            SteamAPI.RunCallbacks();
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            IsInitialized = false;
        }
    }

    public void Shutdown()
    {
        if (!IsInitialized)
        {
            return;
        }

        try
        {
            SteamAPI.Shutdown();
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
        }
        finally
        {
            IsInitialized = false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Shutdown();
        disposed = true;
    }
}
