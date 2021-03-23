[System.Serializable]
public class BADNetworkMessage
{
   public string _opCode;
   public string _playerSessionId;

   public BADNetworkMessage(string opCode, string playerSessionId)
   {
      _opCode = opCode;
      _playerSessionId = playerSessionId;
   }
}
