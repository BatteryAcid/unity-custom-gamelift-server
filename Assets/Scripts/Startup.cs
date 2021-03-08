using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Startup : MonoBehaviour
{
   // Helper function for getting the command line arguments
   // src: https://stackoverflow.com/a/45578115/1956540
   public static bool IsArgFlagPresent(string name)
   {
      var args = System.Environment.GetCommandLineArgs();
      for (int i = 0; i < args.Length; i++)
      {
         // Debug.Log("Arg: " + args[i]);
         if (args[i] == name)
         {
            return true;
         }
      }
      return false;
   }
}
