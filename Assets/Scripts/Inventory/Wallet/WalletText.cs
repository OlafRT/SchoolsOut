using UnityEngine;
using TMPro;

public class WalletText : MonoBehaviour
{
    public PlayerWallet wallet;     // assign your PlayerWallet asset
    public TMP_Text label;          // assign the $ text label
    public string prefix = "";    // shown before the number

    void OnEnable()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (wallet) wallet.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (wallet) wallet.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        if (!wallet || !label) return;
        // N0 = thousands separators, no decimals
        label.text = prefix + wallet.dollars.ToString("N0");
    }
}
