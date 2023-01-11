using Firebase.Analytics;
using UnityEngine;

namespace FirebaseHelper.Runtime.UI
{
    [CreateAssetMenu(fileName = "EV_FB_Analytic_", menuName = "Firebase/Analytic/Event")]
    public class AnalyticEvent : ScriptableObject
    {
        [SerializeField]
        private string _eventName = "egi_btn__click";

        public void Log()
        {
            FirebaseAnalytics.LogEvent(_eventName);
        }
    }
}