using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using EnjoylixSDK.Config;
using EnjoylixSDK.Purchase.Models;
using TMPro;

namespace EnjoylixSDK.Samples
{
    public class EnjoylixDemoController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EnjoylixConfig config;

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

            _sdk = new EnjoylixSdk(config);

            // 2. Subscribe to Events
            _sdk.Auth.OnAuthStateChanged += UpdateAuthStateUI;
            _sdk.Auth.OnAuthError += (e) => LogError($"Auth Error: {e.Message}");
            _sdk.Auth.OnTokenReceived += (t) => Log($"Token received (len: {t.Length})");
            _sdk.Auth.OnLoginCompleted += (kind, success, isRestore) => 
                Log($"Login Completed: Kind={kind}, Success={success}, IsRestore={isRestore}");

            // 3. Subscribe to Payment Events (Global Listener)
            _sdk.PurchaseFlow.OnPaymentResult += (result) => 
            {
                Log($"[Global Payment Listener] Success: {result.success}, ID: {result.payment_id}");
            };

            // 4. Async Init (Restores session if saved)
            try
            {
                await _sdk.InitializeAsync();
                Log("SDK Initialized.");
            }
            catch (Exception e)
            {
                LogError($"Init failed: {e.Message}");
            }
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
                var guestId = await _sdk.Auth.LoginGuestAsync(idToUse);
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
                var session = await _sdk.Auth.LoginOpenIdAsync();
                Log($"OpenID Login Success! User: {session.me.email}");
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
                var session = await _sdk.Auth.LinkGuestAsync(idToUse);
                Log($"Linking Success! Now User: {session.me.email}");
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }

        private void OnLogoutClicked()
        {
            _sdk.Auth.Logout();
            Log("Logged out.");
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

                if (result.success)
                {
                    Log($"<color=green>PURCHASE SUCCESS!</color> Payment ID: {result.payment_id}");
                }
                else
                {
                    Log($"<color=orange>Purchase Not Completed.</color> Reason: {result.error}");
                }
            }
            catch (Exception e)
            {
                LogError($"Purchase Exception: {e.Message}");
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

        private void UpdateAuthStateUI(EnjoylixSDK.Auth.Models.AuthState state)
        {
            txtAuthState.text = $"State: {state}";
            
            bool isGuest = state == EnjoylixSDK.Auth.Models.AuthState.Guest;
            bool isAuth = state == EnjoylixSDK.Auth.Models.AuthState.Authorized;
            bool isSignedOut = state == EnjoylixSDK.Auth.Models.AuthState.SignedOut;

            btnGuestLogin.interactable = isSignedOut;
            btnOpenIdLogin.interactable = isSignedOut || isGuest;
            btnLinkGuest.interactable = isGuest;
            btnLogout.interactable = !isSignedOut;

            btnBuyCoins.interactable = true; 

            if (isAuth && _sdk.Auth.Me != null)
            {
                txtUserInfo.text = $"User: {_sdk.Auth.Me.email}\nID: {_sdk.Auth.Me.id}";
            }
            else if (isGuest)
            {
                txtUserInfo.text = $"Guest ID: {_sdk.Auth.GuestId}";
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