using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;

namespace game 
{
  public static class Extensions 
  {
    public static GameObject GetChild(this Component c, string name)
    {
      return c.gameObject.GetChild(name);
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

    public static void GetComponentsInChildrenRecursive<T>(this Component self, ref List<T> res) where T : Component
    {
      T comp = self.GetComponent<T>();
      if(comp != null)
        res.Add(comp);
      
      var transform = self.transform;
      for(int i = 0; i < transform.childCount; i++)
        transform.GetChild(i).GetComponentsInChildrenRecursive(ref res);
    }

    public static void GetComponentsInChildrenRecursive<T>(this Component self, List<T> res) where T : Component
    {
      self.GetComponentsInChildren<T>(includeInactive: true, result: res);
    }

    static List<GraphicRaycaster> raycasters_buffer = new List<GraphicRaycaster>();
    static List<Button> buttons_buffer = new List<Button>();

    public static void EnableUIInput(this Component c, bool enable)
    {
      c.GetComponentsInChildrenRecursive<GraphicRaycaster>(raycasters_buffer);
      c.GetComponentsInChildrenRecursive<Button>(buttons_buffer);

      for(int i = 0; i < raycasters_buffer.Count; i++)
        raycasters_buffer[i].enabled = enable;
      
      for(int i = 0; i < buttons_buffer.Count; i++)
        buttons_buffer[i].interactable = enable;

      raycasters_buffer.Clear();
      buttons_buffer.Clear();
    }

    public static void AddUnique<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, TValue value)
    {
      if(self.ContainsKey(key))
        return;
      
      self.Add(key, value);
    }
  }
}
