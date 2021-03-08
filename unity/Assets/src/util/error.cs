using System;
 
namespace game 
{
  public class Error
  {
    public static void Assert(bool condition, string msg)
    {
      if(!condition) throw new Exception(msg);
    }

    public static void Verify(bool condition)
    {
      if(!condition) throw new Exception();
    }

    public static void Verify(bool condition, string fmt, params object[] vals)
    {
      if(!condition) throw new Exception(string.Format(fmt, vals));
    }

    public static void Verify(bool condition, string msg)
    {
      if(!condition) throw new Exception(msg);
    }
  }
}