using UnityEngine;

namespace BloodlinesUI
{
    /// <summary>
    /// Simple class for quick DontDestroyOnLoad application
    /// </summary>
    public class FastDontDestroyOnLoad : MonoBehaviour
    {
        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
