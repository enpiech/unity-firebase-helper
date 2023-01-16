using System;
using Enpiech.Core.Runtime;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace FirebaseHelper.Runtime
{
    public sealed class GoogleAdMobController : Singleton<GoogleAdMobController>
    {
        public UnityEvent OnAdLoadedEvent = new();
        public UnityEvent OnAdFailedToLoadEvent = new();
        public UnityEvent OnAdOpeningEvent = new();
        public UnityEvent OnAdFailedToShowEvent = new();
        public UnityEvent OnUserEarnedRewardEvent = new();
        public UnityEvent OnAdClosedEvent = new();
        public bool showFpsMeter = true;

        [CanBeNull]
        public TextMeshProUGUI fpsMeter;

        [CanBeNull]
        public TextMeshProUGUI statusText;

        private readonly TimeSpan APPOPEN_TIMEOUT = TimeSpan.FromHours(4);

        [CanBeNull]
        private AppOpenAd appOpenAd;

        private DateTime appOpenExpireTime;

        [CanBeNull]
        private BannerView bannerView;

        private float deltaTime;

        [CanBeNull]
        private InterstitialAd interstitialAd;

        private bool isShowingAppOpenAd;
        private RewardedAd rewardedAd;

        [CanBeNull]
        private RewardedInterstitialAd rewardedInterstitialAd;

    #region HELPER METHODS

        private static AdRequest CreateAdRequest()
        {
            return new AdRequest.Builder()
                .AddKeyword("unity-admob-sample")
                .Build();
        }

    #endregion

    #region AD INSPECTOR

        public void OpenAdInspector()
        {
            PrintStatus("Open ad Inspector.");

            MobileAds.OpenAdInspector(error =>
            {
                if (error != null)
                {
                    PrintStatus("ad Inspector failed to open with error: " + error);
                }
                else
                {
                    PrintStatus("Ad Inspector opened successfully.");
                }
            });
        }

    #endregion

    #region Utility

        /// <summary>
        ///     Log the message and update the status text on the main thread.
        ///     <summary>
        private void PrintStatus(string message)
        {
            Debug.Log(message);
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (statusText != null)
                {
                    statusText.text = message;
                }
            });
        }

    #endregion

    #region UNITY MONOBEHAVIOR METHODS

        public void Start()
        {
#if APPLOVIN
            AppLovin.Initialize()
            AppLovin.SetHasUserConsent(true);
            AppLovin.SetIsAgeRestrictedUser(true);
            AppLovin.SetDoNotSell(true);
#endif
            MobileAds.SetiOSAppPauseOnBackground(true);
#if DEV
            var deviceIds = new List<string> { AdRequest.TestDeviceSimulator };

            // Add some test device IDs (replace with your own device IDs).
#if UNITY_IPHONE
            deviceIds.Add("96e23e80653bb28980d3f40beb58915c");
#elif UNITY_ANDROID
            deviceIds.Add("75EF8D155528C04DACBBA6F36F433035");
#endif

#endif

            // Configure TagForChildDirectedTreatment and test device IDs.
            var requestConfiguration =
                new RequestConfiguration.Builder()
                    .SetTagForChildDirectedTreatment(TagForChildDirectedTreatment.True)
#if DEV
                    .SetTestDeviceIds(deviceIds)
#endif
                    .build();
            MobileAds.SetRequestConfiguration(requestConfiguration);

            // Initialize the Google Mobile Ads SDK.
            MobileAds.Initialize(HandleInitCompleteAction);

            // Listen to application foreground / background events.
            AppStateEventNotifier.AppStateChanged += OnAppStateChanged;
        }

        private void HandleInitCompleteAction(InitializationStatus initializationStatus)
        {
            Debug.Log("Initialization complete.");

            // Callbacks from GoogleMobileAds are not guaranteed to be called on
            // the main thread.
            // In this example we use MobileAdsEventExecutor to schedule these calls on
            // the next Update() loop.
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (statusText != null)
                {
                    statusText.text = "Initialization complete.";
                }
                RequestBannerAd();
                RequestAndLoadRewardedAd();
            });
        }

        private void Update()
        {
            if (fpsMeter == null)
            {
                return;
            }
            if (showFpsMeter)
            {
                fpsMeter.gameObject.SetActive(true);
                deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
                var fps = 1.0f / deltaTime;
                fpsMeter.text = $"{fps:0.} fps";
            }
            else
            {
                fpsMeter.gameObject.SetActive(false);
            }
        }

    #endregion

    #region BANNER ADS

        public void RequestBannerAd()
        {
            PrintStatus("Requesting Banner ad.");

            // These ad units are configured to always serve test ads.
#if UNITY_EDITOR
            const string adUnitId = "unused";
#elif UNITY_ANDROID && DEV
            const string adUnitId = "ca-app-pub-3940256099942544/6300978111";
#elif UNITY_ANDROID && PROD
            const string adUnitId = "ca-app-pub-3423586166659435/6425222839";
#elif UNITY_IPHONE && DEV
            const string adUnitId = "ca-app-pub-3940256099942544/2934735716";
#elif UNITY_IPHONE && PROD
            const string adUnitId = "ca-app-pub-3423586166659435/9439803957";
#else
            const string adUnitId = "unexpected_platform";
#endif

            // Clean up banner before reusing
            bannerView?.Destroy();

            // Create a 320x50 banner at top of the screen
            bannerView = new BannerView(adUnitId, AdSize.Banner, AdPosition.Bottom);

            // Add Event Handlers
            bannerView.OnAdLoaded += (_, _) =>
            {
                PrintStatus("Banner ad loaded.");
                OnAdLoadedEvent?.Invoke();
            };
            bannerView.OnAdFailedToLoad += (_, args) =>
            {
                PrintStatus("Banner ad failed to load with error: " + args.LoadAdError.GetMessage());
                OnAdFailedToLoadEvent?.Invoke();
            };
            bannerView.OnAdOpening += (_, _) =>
            {
                PrintStatus("Banner ad opening.");
                OnAdOpeningEvent?.Invoke();
            };
            bannerView.OnAdClosed += (_, _) =>
            {
                PrintStatus("Banner ad closed.");
                OnAdClosedEvent?.Invoke();
            };
            bannerView.OnPaidEvent += (_, args) =>
            {
                var msg = $"Banner ad received a paid event. (currency: {args.AdValue.CurrencyCode}, value: {args.AdValue.Value}";
                PrintStatus(msg);
            };

            // Load a banner ad
            bannerView.LoadAd(CreateAdRequest());
        }

        public void DestroyBannerAd()
        {
            bannerView?.Destroy();
        }

    #endregion

    #region INTERSTITIAL ADS

        public void RequestAndLoadInterstitialAd()
        {
            PrintStatus("Requesting Interstitial ad.");

#if UNITY_EDITOR
            const string adUnitId = "unused";
#elif UNITY_ANDROID
            const string adUnitId = "ca-app-pub-3940256099942544/1033173712";
#elif UNITY_IOS
            const string adUnitId = "ca-app-pub-3940256099942544/4411468910";
#else
            const string adUnitId = "unexpected_platform";
#endif

            // Clean up interstitial before using it
            interstitialAd?.Destroy();

            interstitialAd = new InterstitialAd(adUnitId);

            // Add Event Handlers
            interstitialAd.OnAdLoaded += (_, _) =>
            {
                PrintStatus("Interstitial ad loaded.");
                OnAdLoadedEvent?.Invoke();
            };
            interstitialAd.OnAdFailedToLoad += (_, args) =>
            {
                PrintStatus("Interstitial ad failed to load with error: " + args.LoadAdError.GetMessage());
                OnAdFailedToLoadEvent?.Invoke();
            };
            interstitialAd.OnAdOpening += (_, _) =>
            {
                PrintStatus("Interstitial ad opening.");
                OnAdOpeningEvent?.Invoke();
            };
            interstitialAd.OnAdClosed += (_, _) =>
            {
                PrintStatus("Interstitial ad closed.");
                OnAdClosedEvent?.Invoke();
            };
            interstitialAd.OnAdDidRecordImpression += (_, _) => { PrintStatus("Interstitial ad recorded an impression."); };
            interstitialAd.OnAdFailedToShow += (_, _) => { PrintStatus("Interstitial ad failed to show."); };
            interstitialAd.OnPaidEvent += (_, args) =>
            {
                var msg = $"Interstitial ad received a paid event. (currency: {args.AdValue.CurrencyCode}, value: {args.AdValue.Value}";
                PrintStatus(msg);
            };

            // Load an interstitial ad
            interstitialAd.LoadAd(CreateAdRequest());
        }

        public void ShowInterstitialAd()
        {
            if (interstitialAd != null && interstitialAd.IsLoaded())
            {
                interstitialAd.Show();
            }
            else
            {
                PrintStatus("Interstitial ad is not ready yet.");
            }
        }

        public void DestroyInterstitialAd()
        {
            interstitialAd?.Destroy();
        }

    #endregion

    #region REWARDED ADS

        public bool HasRewardedAd => rewardedAd != null && rewardedAd.IsLoaded();

        public void RequestAndLoadRewardedAd()
        {
            PrintStatus("Requesting Rewarded ad.");
#if UNITY_EDITOR
            const string adUnitId = "unused";
#elif UNITY_ANDROID && DEV
            const string adUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_ANDROID && PROD
            const string adUnitId = "ca-app-pub-3423586166659435/1894053097";
#elif UNITY_IPHONE && DEV
            const string adUnitId = "ca-app-pub-3940256099942544/1712485313";
#elif UNITY_IPHONE && PROD
            const string adUnitId = "ca-app-pub-3423586166659435/7230140258";
#else
            const string adUnitId = "unexpected_platform";
#endif

            // create new rewarded ad instance
            rewardedAd = new RewardedAd(adUnitId);

            // Add Event Handlers
            rewardedAd.OnAdLoaded += (_, _) =>
            {
                PrintStatus("Reward ad loaded.");
                OnAdLoadedEvent?.Invoke();
            };
            rewardedAd.OnAdFailedToLoad += (_, _) =>
            {
                PrintStatus("Reward ad failed to load.");
                OnAdFailedToLoadEvent?.Invoke();
            };
            rewardedAd.OnAdOpening += (_, _) =>
            {
                PrintStatus("Reward ad opening.");
                OnAdOpeningEvent?.Invoke();
            };
            rewardedAd.OnAdFailedToShow += (_, args) =>
            {
                PrintStatus("Reward ad failed to show with error: " + args.AdError.GetMessage());
                OnAdFailedToShowEvent.Invoke();
            };
            rewardedAd.OnAdClosed += (_, _) =>
            {
                PrintStatus("Reward ad closed.");
                OnAdClosedEvent?.Invoke();
            };
            rewardedAd.OnUserEarnedReward += (_, args) =>
            {
                PrintStatus("User earned Reward ad reward: " + args.Amount);
                OnUserEarnedRewardEvent.Invoke();
            };
            rewardedAd.OnAdDidRecordImpression += (_, _) => { PrintStatus("Reward ad recorded an impression."); };
            rewardedAd.OnPaidEvent += (_, args) =>
            {
                var msg = $"Rewarded ad received a paid event. (currency: {args.AdValue.CurrencyCode}, value: {args.AdValue.Value}";
                PrintStatus(msg);
            };

            // Create empty ad request
            rewardedAd.LoadAd(CreateAdRequest());
        }

        public void ShowRewardedAd()
        {
            if (rewardedAd != null)
            {
                rewardedAd.Show();
            }
            else
            {
                PrintStatus("Rewarded ad is not ready yet.");
            }
        }

        public void RequestAndLoadRewardedInterstitialAd()
        {
            PrintStatus("Requesting Rewarded Interstitial ad.");

            // These ad units are configured to always serve test ads.
#if UNITY_EDITOR
            const string adUnitId = "unused";
#elif UNITY_ANDROID
        const string adUnitId = "ca-app-pub-3940256099942544/5354046379";
#elif UNITY_IPHONE
        const string adUnitId = "ca-app-pub-3940256099942544/6978759866";
#else
        const string adUnitId = "unexpected_platform";
#endif

            // Create an interstitial.
            RewardedInterstitialAd.LoadAd(adUnitId, CreateAdRequest(), (rewardedInterstitialAd, error) =>
            {
                if (error != null)
                {
                    PrintStatus("Rewarded Interstitial ad load failed with error: " + error);
                    return;
                }

                this.rewardedInterstitialAd = rewardedInterstitialAd;
                PrintStatus("Rewarded Interstitial ad loaded.");

                // Register for ad events.
                this.rewardedInterstitialAd.OnAdDidPresentFullScreenContent += (_, _) =>
                {
                    PrintStatus("Rewarded Interstitial ad presented.");
                };
                this.rewardedInterstitialAd.OnAdDidDismissFullScreenContent += (_, _) =>
                {
                    PrintStatus("Rewarded Interstitial ad dismissed.");
                    this.rewardedInterstitialAd = null;
                };
                this.rewardedInterstitialAd.OnAdFailedToPresentFullScreenContent += (_, args) =>
                {
                    PrintStatus("Rewarded Interstitial ad failed to present with error: " +
                                args.AdError.GetMessage());
                    this.rewardedInterstitialAd = null;
                };
                this.rewardedInterstitialAd.OnPaidEvent += (_, args) =>
                {
                    var msg =
                        $"Rewarded Interstitial ad received a paid event. (currency: {args.AdValue.CurrencyCode}, value: {args.AdValue.Value}";
                    PrintStatus(msg);
                };
                this.rewardedInterstitialAd.OnAdDidRecordImpression += (_, _) =>
                {
                    PrintStatus("Rewarded Interstitial ad recorded an impression.");
                };
            });
        }

        public void ShowRewardedInterstitialAd()
        {
            if (rewardedInterstitialAd != null)
            {
                rewardedInterstitialAd.Show(reward => { PrintStatus("Rewarded Interstitial ad Rewarded : " + reward.Amount); });
            }
            else
            {
                PrintStatus("Rewarded Interstitial ad is not ready yet.");
            }
        }

    #endregion

    #region APPOPEN ADS

        public bool IsAppOpenAdAvailable => !isShowingAppOpenAd
                                            && appOpenAd != null
                                            && DateTime.Now < appOpenExpireTime;

        public void OnAppStateChanged(AppState state)
        {
            // Display the app open ad when the app is foregrounded.
            Debug.Log("App State is " + state);

            // OnAppStateChanged is not guaranteed to execute on the Unity UI thread.
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (state == AppState.Foreground)
                {
                    ShowAppOpenAd();
                }
            });
        }

        public void RequestAndLoadAppOpenAd()
        {
            PrintStatus("Requesting App Open ad.");
#if UNITY_EDITOR
            const string adUnitId = "unused";
#elif UNITY_ANDROID
        const string adUnitId = "ca-app-pub-3940256099942544/3419835294";
#elif UNITY_IPHONE
        const string adUnitId = "ca-app-pub-3940256099942544/5662855259";
#else
        const string adUnitId = "unexpected_platform";
#endif
            // create new app open ad instance
            AppOpenAd.LoadAd(adUnitId,
                ScreenOrientation.Portrait,
                CreateAdRequest(),
                OnAppOpenAdLoad);
        }

        private void OnAppOpenAdLoad(AppOpenAd ad, [CanBeNull] AdFailedToLoadEventArgs error)
        {
            if (error != null)
            {
                PrintStatus("App Open ad failed to load with error: " + error);
                return;
            }

            PrintStatus("App Open ad loaded. Please background the app and return.");
            appOpenAd = ad;
            appOpenExpireTime = DateTime.Now + APPOPEN_TIMEOUT;
        }

        public void ShowAppOpenAd()
        {
            if (!IsAppOpenAdAvailable)
            {
                return;
            }

            // Register for ad events.
            appOpenAd.OnAdDidDismissFullScreenContent += (_, _) =>
            {
                PrintStatus("App Open ad dismissed.");
                isShowingAppOpenAd = false;
                if (appOpenAd == null)
                {
                    return;
                }
                appOpenAd.Destroy();
                appOpenAd = null;
            };
            appOpenAd.OnAdFailedToPresentFullScreenContent += (_, args) =>
            {
                PrintStatus("App Open ad failed to present with error: " + args.AdError.GetMessage());

                isShowingAppOpenAd = false;
                if (appOpenAd == null)
                {
                    return;
                }

                appOpenAd.Destroy();
                appOpenAd = null;
            };
            appOpenAd.OnAdDidPresentFullScreenContent += (_, _) => { PrintStatus("App Open ad opened."); };
            appOpenAd.OnAdDidRecordImpression += (_, _) => { PrintStatus("App Open ad recorded an impression."); };
            appOpenAd.OnPaidEvent += (_, args) =>
            {
                var msg = $"App Open ad received a paid event. (currency: {args.AdValue.CurrencyCode}, value: {args.AdValue.Value}";
                PrintStatus(msg);
            };

            isShowingAppOpenAd = true;
            appOpenAd.Show();
        }

    #endregion
    }
}