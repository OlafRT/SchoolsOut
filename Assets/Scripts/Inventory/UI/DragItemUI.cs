using UnityEngine;
using UnityEngine.UI;

public class DragItemUI : MonoBehaviour {
    public Canvas canvas; public Image ghostIcon; private ItemInstance draggingItem; private Inventory inv; private int fromIndex=-1;
    void Update(){ if(ghostIcon.gameObject.activeSelf) ghostIcon.transform.position = Input.mousePosition; }
    public void BeginDrag(Inventory inventory, int index){ var s=inventory.Slots[index]; if(s.IsEmpty) return; inv=inventory; fromIndex=index; draggingItem=s.item; ghostIcon.sprite=s.item.Icon; ghostIcon.gameObject.SetActive(true);}    
    public void EndDragTo(int toIndex){ if(inv==null) return; inv.Move(fromIndex,toIndex); ghostIcon.gameObject.SetActive(false); draggingItem=null; inv=null; fromIndex=-1; }
}

