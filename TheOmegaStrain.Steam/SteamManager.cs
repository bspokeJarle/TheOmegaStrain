using Steamworks;

namespace TheOmegaStrain.Steam;

public sealed class SteamManager : IDisposable
{
    private bool disposed;

    public bool IsInitialized { get; private set; }

    public bool IsAvailable => IsInitialized;

    public string? LastError { get; private set; }

    public bool IsSteamRunning
    {
        get
        {
            try
            {
                return SteamAPI.IsSteamRunning();
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }
    }

    public bool IsOverlayEnabled
    {
        get
        {
            if (!IsInitialized)
            {
                return false;
            }

            try
            {
                return SteamUtils.IsOverlayEnabled();
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }
    }

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
            if (appId > 0 &&
                !HasLocalSteamAppIdFile() &&
                SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
            {
                LastError = "Steam restart requested.";
                return false;
            }

            var initResult = SteamAPI.InitEx(out var steamError);
            IsInitialized = initResult == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
            LastError = IsInitialized
                ? null
                : string.IsNullOrWhiteSpace(steamError)
                    ? $"SteamAPI.InitEx returned {initResult}."
                    : $"SteamAPI.InitEx returned {initResult}: {steamError}";
            return IsInitialized;
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            IsInitialized = false;
            return false;
        }
    }

    private static bool HasLocalSteamAppIdFile()
    {
        return File.Exists(Path.Combine(AppContext.BaseDirectory, "steam_appid.txt")) ||
               File.Exists(Path.Combine(Environment.CurrentDirectory, "steam_appid.txt"));
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
