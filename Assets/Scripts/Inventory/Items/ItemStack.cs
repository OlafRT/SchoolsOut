using System;

[Serializable]
public class ItemStack {
    public ItemInstance item; public int count;
    public bool IsEmpty => item == null || count <= 0;
    public ItemStack(ItemInstance item, int count){ this.item = item; this.count = count; }
    public ItemStack Clone() => new ItemStack(item, count);
}
