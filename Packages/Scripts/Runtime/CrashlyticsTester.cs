using System;
using UnityEngine;

namespace FirebaseHelper.Runtime
{
    public class CrashlyticsTester : MonoBehaviour
    {
        private int _updatesBeforeException;

        // Use this for initialization
        private void Start()
        {
            _updatesBeforeException = 0;
        }

        // Update is called once per frame
        private void Update()
        {
            // Call the exception-throwing method here so that it's run
            // every frame update
            throwExceptionEvery60Updates();
        }

        // A method that tests your Crashlytics implementation by throwing an
        // exception every 60 frame updates. You should see non-fatal errors in the
        // Firebase console a few minutes after running your app with this method.
        private void throwExceptionEvery60Updates()
        {
            if (_updatesBeforeException > 0)
            {
                _updatesBeforeException--;
            }
            else
            {
                // Set the counter to 60 updates
                _updatesBeforeException = 60;

                // Throw an exception to test your Crashlytics implementation
                throw new Exception("test exception please ignore");
            }
        }
    }
}