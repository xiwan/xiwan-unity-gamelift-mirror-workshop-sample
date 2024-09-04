using UnityEngine;
using System.Collections;
using Mirror;

using System;

namespace Mirror.Examples.Tanks
{
  public class Stats : NetworkBehaviour
  {

    [SyncVar]
    int avgFrameRate = 0;

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void FixedUpdate()
    {
      avgFrameRate = (int)(1f / Time.unscaledDeltaTime);
      //fps.text = "fps: " + Math.Round(avgFrameRate, 0);

      //Debug.Log();
    }
  }
}



