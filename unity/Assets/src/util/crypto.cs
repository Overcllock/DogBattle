using System.Text;

namespace game {

public static class Hash
{
  static byte[] hash_bytes = new byte[512];

  public static uint CRC32(string id)
  {
    var encoding = Encoding.ASCII;
    var byte_len = encoding.GetByteCount(id);
    var buffer = byte_len <= hash_bytes.Length ? hash_bytes : new byte[byte_len];
    encoding.GetBytes(id, 0, id.Length, buffer, 0);
    return Crc32.Compute(buffer, id.Length);
  }
}

}