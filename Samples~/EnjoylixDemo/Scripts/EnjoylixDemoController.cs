using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using EnjoylixSDK.Auth.Models;
using EnjoylixSDK.Config;
using EnjoylixSDK.Net;
using EnjoylixSDK.Purchase.Models;
using TMPro;

namespace EnjoylixSDK.Samples
{
    public class EnjoylixDemoController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EnjoylixConfig config;
        [Tooltip("Platform string agreed with Enjoylix. Empty = Application.platform.")]
        [SerializeField] private string platform;

        [Header("UI References - Auth")]
        [SerializeField] private Toggle toggleRandomDeviceId;
        [SerializeField] private Button btnGuestLogin;
        [SerializeField] private Button btnOpenIdLogin;
        [SerializeField] private Button btnLinkGuest;
        [SerializeField] private Button btnLogout;
        [SerializeField] private TextMeshProUGUI txtAuthState;
        [SerializeField] private TextMeshProUGUI txtUserInfo;

        [Header("UI References - Payments")]
        [SerializeField] private Button btnBuyCoins;

        [Header("UI References - Attribution")]
        [SerializeField] private Button btnTrackTutorial;
        [SerializeField] private Button btnTrackPurchase;

        [Header("UI References - Logs")]
        [SerializeField] private TextMeshProUGUI txtLogs;
        [SerializeField] private ScrollRect scrollRect;

        private EnjoylixSdk _sdk;
        private StringBuilder _logBuilder = new StringBuilder();

        private string _cachedRandomId;
        private string _currentDeviceId
        {
            get
            {
                if (toggleRandomDeviceId != null && toggleRandomDeviceId.isOn)
                {
                    return _cachedRandomId;
                }
                return SystemInfo.deviceUniqueIdentifier;
            }
        }

        private async void Start()
        {
            // 0. Setup UI
            SetupButtons();
            _cachedRandomId = Guid.NewGuid().ToString();

            // 1. Initialize SDK
            Log("Initializing SDK...");
            if (config == null)
            {
                LogError("EnjoylixConfig is missing! Please assign it in the inspector.");
                return;
            }

            var platformId = string.IsNullOrEmpty(platform) ? Application.platform.ToString() : platform;
            _sdk = new EnjoylixSdk(config, platformId);

            // 2. Subscribe to Events
            _sdk.OnLoginCompleted += (kind, success) =>
            {
                Log($"Login Completed: Kind={kind}, Success={success}");
                UpdateAuthStateUI();
            };
            _sdk.OnPopupBlocked += url => LogError($"Popup blocked, open it manually: {url}");

            // 3. Load the stored session (synchronous, no network, does not log in)
            _sdk.Initialize();
            Log("SDK Initialized.");

            // 4. Restore the stored session, if any.
            // A real game would call _sdk.LoginOnStartAsync(deviceId) here instead — it restores
            // the session or opens the browser login. The demo keeps login on the buttons.
            if (!string.IsNullOrEmpty(_sdk.AccessToken))
            {
                try
                {
                    await _sdk.EnsureValidTokenAsync();
                    Log("Stored session restored.");
                }
                catch (InvalidOperationException)
                {
                    Log("Stored session expired, please log in again.");
                }
                catch (Exception e)
                {
                    LogError($"Session restore failed: {e.Message}");
                }
            }

            UpdateAuthStateUI();
        }

        private void SetupButtons()
        {
            btnGuestLogin.onClick.AddListener(OnGuestLoginClicked);
            btnOpenIdLogin.onClick.AddListener(OnOpenIdLoginClicked);
            btnLinkGuest.onClick.AddListener(OnLinkGuestClicked);
            btnLogout.onClick.AddListener(OnLogoutClicked);

            btnBuyCoins.onClick.AddListener(OnBuyCoinsClicked);

            btnTrackTutorial.onClick.AddListener(OnTrackTutorialClicked);
            btnTrackPurchase.onClick.AddListener(OnTrackPurchaseClicked);
        }

        #region Auth Handlers

        private async void OnGuestLoginClicked()
        {
            Log("Starting Guest Login...");
            try
            {
                var idToUse = _currentDeviceId;
                Log($"Using ID: {idToUse}");
                var guestId = await _sdk.LoginGuestAsync(idToUse);
                Log($"Guest Login Success! ID: {guestId}");
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }

        private async void OnOpenIdLoginClicked()
        {
            Log("Starting OpenID Login (Browser)...");
            try
            {
                var session = await _sdk.LoginOpenIdAsync();
                if (session == null) return; // popup blocked, OnPopupBlocked has fired

                Log($"OpenID Login Success! User ID: {session.me?.id}");
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }

        private async void OnLinkGuestClicked()
        {
            Log("Starting Guest Linking...");
            try
            {
                var idToUse = _currentDeviceId;
                Log($"Using ID: {idToUse}");
                var session = await _sdk.LinkGuestAsync(idToUse);
                if (session == null) return; // popup blocked, OnPopupBlocked has fired

                Log($"Linking Success! Now User ID: {session.me?.id}");
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }

        private void OnLogoutClicked()
        {
            _sdk.Logout();
            Log("Logged out.");
            UpdateAuthStateUI();
        }

        #endregion

        #region Payment Handlers

        private async void OnBuyCoinsClicked()
        {
            Log("=== Starting Purchase Flow ===");

            var request = new PurchaseCreateRequest
            {
                coin_amount = 100,
                game_name = config.ProjectName,
                transaction_id = Guid.NewGuid().ToString("N"),
                transaction_name = "Demo 100 Coins",
                transaction_image_url = "https://placehold.co/128x128/orange/white?text=Gold"
            };

            Log($"Creating Order: {request.transaction_name} ({request.coin_amount} coins)...");

            try
            {
                var result = await _sdk.PurchaseCreateAsync(request, TimeSpan.FromMinutes(5));

                if (result == null)
                {
                    Log("<color=orange>Purchase Not Started.</color> The payment window was blocked.");
                }
                else if (result.success)
                {
                    Log($"<color=green>PURCHASE SUCCESS!</color> Payment ID: {result.payment_id}");
                }
                else
                {
                    Log($"<color=orange>Purchase Not Completed.</color> Reason: {result.error}");
                }
            }
            catch (UserDismissedException)
            {
                Log("<color=orange>Purchase Dismissed.</color> The player returned without paying.");
            }
            catch (UserCancelledException)
            {
                Log("<color=orange>Purchase Cancelled.</color> The player declined to open the payment window.");
            }
            catch (FlowTimeoutException e)
            {
                LogError($"Purchase timed out after {e.Timeout}.");
            }
            catch (Exception e)
            {
                LogError($"Purchase Exception: {e.Message}");
            }
            finally
            {
                // The purchase re-authenticates when there is no session, refresh the UI either way
                UpdateAuthStateUI();
            }
        }

        #endregion

        #region Attribution Handlers

        private void OnTrackTutorialClicked()
        {
            _sdk.TrackTutorialComplete();
            Log("Event Sent: Tutorial Complete");
        }

        private void OnTrackPurchaseClicked()
        {
            string txId = Guid.NewGuid().ToString().Substring(0, 8);
            _sdk.TrackPurchase(
                eventType: "purchase",
                source: "shop_gold_pack",
                checkoutId: "com.game.gold.100",
                transactionId: txId,
                amount: 4.99f,
                currencyCode: "USD",
                isQaPurchase: true
            );
            Log($"Event Sent: Purchase (Tx: {txId})");
        }

        #endregion

        #region UI Updates

        private AuthState CurrentAuthState()
        {
            if (string.IsNullOrEmpty(_sdk.AccessToken)) return AuthState.SignedOut;
            return _sdk.IsEmailLogin ? AuthState.Authorized : AuthState.Guest;
        }

        private void UpdateAuthStateUI()
        {
            var state = CurrentAuthState();
            txtAuthState.text = $"State: {state}";

            bool isGuest = state == AuthState.Guest;
            bool isAuth = state == AuthState.Authorized;
            bool isSignedOut = state == AuthState.SignedOut;

            btnGuestLogin.interactable = isSignedOut;
            btnOpenIdLogin.interactable = isSignedOut || isGuest;
            btnLinkGuest.interactable = isGuest;
            btnLogout.interactable = !isSignedOut;

            btnBuyCoins.interactable = true;

            if (isAuth)
            {
                txtUserInfo.text = $"User ID: {_sdk.EnjoylixId}";
            }
            else if (isGuest)
            {
                txtUserInfo.text = $"Guest ID: {_sdk.EnjoylixId}";
            }
            else
            {
                txtUserInfo.text = "Not logged in";
            }
        }

        private void Log(string msg)
        {
            Debug.Log($"[Demo] {msg}");
            _logBuilder.AppendLine($"> {msg}");
            if (txtLogs)
            {
                txtLogs.text = _logBuilder.ToString();
                // Auto scroll to bottom
                Canvas.ForceUpdateCanvases();
                if(scrollRect) scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void LogError(string msg)
        {
            Debug.LogError($"[Demo] {msg}");
            _logBuilder.AppendLine($"<color=red>ERROR: {msg}</color>");
            if (txtLogs) txtLogs.text = _logBuilder.ToString();
        }

        #endregion
    }
}
