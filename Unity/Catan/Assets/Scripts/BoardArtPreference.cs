using System;
using UnityEngine;

internal static class BoardArtPreference
{
    const string Key="Catan.BoardArt.Models";
    internal static bool Painted {get;private set;}=PlayerPrefs.GetInt(Key,1)==0;
    internal static event Action Changed;

    internal static void Select(bool painted, bool persist = true)
    {
        if(Painted==painted)return;
        Painted=painted;
        if (persist)
        {
            PlayerPrefs.SetInt(Key,painted?0:1);
            PlayerPrefs.Save();
        }
        Changed?.Invoke();
    }
}
