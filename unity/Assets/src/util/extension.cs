using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace game 
{
  public static class Extensions 
  {
    public static GameObject GetChild(this UIWindow window, string name)
    {
      return window.gameObject.GetChild(name);
    }

    public static GameObject GetChild(this GameObject o, string name)
    {
      Transform t = o.transform.Find(name);
      if(t == null)
        Error.Verify(false, "Child not found {0}", name);
      return t.gameObject;
    }

    public static Transform FindRecursive(this Transform current, string name)   
    {
      if(current.parent)
      {
        if(current.parent.Find(name) == current)
          return current;
      }
      else if(current.name == name)
        return current;

      for(int i = 0; i < current.childCount; ++i)
      {
        var chld = current.GetChild(i); 
        var tmp = chld.FindRecursive(name);
        if(tmp != null)
          return tmp;
      }
      return null;
    }
    
    public static T AddComponentOnce<T>(this GameObject self) where T : Component
    {
      T c = self.GetComponent<T>();
      if(c == null)
        c = self.AddComponent<T>();
      return c;
    }

    public static void MakeButton(this GameObject o, UnityAction action, bool set_active = true)
    {
      var button = o.GetComponent<Button>();
      if(button == null)
      {
        Debug.LogError($"Cannot make button from this GameObject: {o.name}.");
        return;
      }

      button.onClick.AddListener(action);
      o.SetActive(set_active);
    }
  }
}
