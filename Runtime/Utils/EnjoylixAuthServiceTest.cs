using System;
using EnjoylixSDK.Auth;
using EnjoylixSDK.Auth.Internal;
using EnjoylixSDK.Common;
using EnjoylixSDK.Config;
using EnjoylixSDK.Net;
using UnityEngine;

namespace EnjoylixSDK.Utils
{
	public class EnjoylixAuthServiceTest : MonoBehaviour
	{
		[SerializeField] private EnjoylixConfig config;

		private AuthService _service;
		private UrlBuilder _urlBuilder;

		private void Awake()
		{
			_urlBuilder = new UrlBuilder(config, "UNITY_EDITOR");
			var webBrowserService = new WebBrowserService();
			_service = new AuthService(_urlBuilder, webBrowserService, config);
			_service.OnTokenReceived += t => Debug.Log($"[TEST] Token len={t?.Length} token={t}");
			_service.OnAuthError += e => Debug.LogException(e);
			_service.OnPopupBlocked += s => Debug.LogWarning("[TEST] Popup was blocked");
		}

		[ContextMenu("Guest Login")]
		public async void GuestLogin()
		{
			try
			{
				var deviceId = Guid.NewGuid().ToString("N");
				var guestId = await _service.LoginGuestAsync(deviceId);
				Debug.Log($"[TEST] GuestId={guestId}");
			}
			catch (Exception e) { Debug.LogException(e); }
		}

		[ContextMenu("OpenID Login")]
		public async void OpenIdLogin()
		{
			try
			{
				var session = await _service.LoginOpenIdAsync(TimeSpan.FromMinutes(5));
				Debug.Log($"[TEST] Me id={session.me.id}");
			}
			catch (Exception e) { Debug.LogException(e); }
		}

		[ContextMenu("Link Guest")]
		public async void LinkGuest()
		{
			try
			{
				var deviceId = Guid.NewGuid().ToString("N");
				await _service.LoginGuestAsync(deviceId);
				var session = await _service.LinkGuestAsync(deviceId, TimeSpan.FromMinutes(5));
				Debug.Log($"[TEST] Linked Me id={session.me.id}");
			}
			catch (Exception e) { Debug.LogException(e); }
		}
	
		[ContextMenu("Game Session Ticket")]
		public async void GameSessionTicket()
		{
			try
			{
				var ticket = await _service.CreateGameSessionTicketAsync();
				Debug.Log($"[TEST] ticket={ticket}");
			}
			catch (Exception e) { Debug.LogException(e); }
		}

		[ContextMenu("Test UrlBuilder")]
		public void TestUrlBuilder()
		{
			Debug.Log("GuestLoginApiUrl: " + _urlBuilder.BuildGuestLoginApiUrl("dev123"));
			Debug.Log("LoginPageUrl: " + _urlBuilder.BuildAuthPageUrl(isRegistration:false));
			Debug.Log("RegisterPageUrl: " + _urlBuilder.BuildAuthPageUrl(isRegistration:true, token:"dev123"));
			Debug.Log("UserInfoUrl: " + _urlBuilder.BuildUserInfoApiUrl());
		}
		
		[ContextMenu("Expire Token")]
		public void ExpireToken()
		{
			//_client.AccessToken = "invalid";
		}
		
		[ContextMenu("Try Restore Token")]
		public void TryRestoreToken()
		{
			//_client.UpdateTokenAsync(destroyCancellationToken);
		}
	}
}