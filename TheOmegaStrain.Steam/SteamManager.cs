using Steamworks;

namespace TheOmegaStrain.Steam;

public sealed class SteamManager : IDisposable
{
    private bool disposed;
    private bool steamInputInitialized;

    public bool IsInitialized { get; private set; }

    public bool IsAvailable => IsInitialized;

    public bool RestartRequested { get; private set; }

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

        RestartRequested = false;

        try
        {
            if (RequestRestartThroughSteamIfNecessary(appId))
            {
                RestartRequested = true;
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

    public static bool RequestRestartThroughSteamIfNecessary(uint appId)
    {
        if (appId == 0 || HasLocalSteamAppIdFile())
        {
            return false;
        }

        try
        {
            // Disabled for local Release runs so the app never asks Steam to relaunch it
            // (which triggers the Windows "open steam:// link" prompt when Steam isn't
            // handling the app). Re-enable for real Steam builds.
            //return SteamAPI.RestartAppIfNecessary(new AppId_t(appId));
            return false;
        }
        catch
        {
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

    // Detection only: how many controllers Steam Input currently reports.
    // Returns 0 when Steam is unavailable so the game stays playable without Steam.
    public int GetConnectedControllerCount()
    {
        if (!IsInitialized)
        {
            return 0;
        }

        try
        {
            if (!steamInputInitialized)
            {
                SteamInput.Init(false);
                steamInputInitialized = true;
            }

            SteamInput.RunFrame();
            var handles = new InputHandle_t[Constants.STEAM_INPUT_MAX_COUNT];
            return Math.Max(0, SteamInput.GetConnectedControllers(handles));
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            return 0;
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
            if (steamInputInitialized)
            {
                SteamInput.Shutdown();
            }

            SteamAPI.Shutdown();
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
        }
        finally
        {
            steamInputInitialized = false;
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
