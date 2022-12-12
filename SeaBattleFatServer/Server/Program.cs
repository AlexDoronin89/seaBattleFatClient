using System;
using ConnectionLibrary;
using ConnectionLibrary.Tools;
using ConnectionLibrary.Entity;
using System.Net.Sockets;

namespace Server
{
    class Program
    {
        

        private static void Main(string[] args)
        {
            int currentStepNumber = 0;

            TcpListener server = ConnectionTools.GetListener();


            Logger.Log("SERVER STARTED");

            TcpClient playerFirstClient = AcceptClient(server, ConstantData.PlayerChars.First);
            TcpClient playerSecondClient = AcceptClient(server, ConstantData.PlayerChars.Second);

            ConnectionTools.SendResponce(playerFirstClient, ConstantData.GameStates.Go);
            ConnectionTools.SendResponce(playerSecondClient, ConstantData.GameStates.Wait);

            TcpClient goingPlayer = playerSecondClient;

            while (playerFirstClient.Connected && playerSecondClient.Connected)
            {

                if (goingPlayer!=playerFirstClient)
                {
                    string requestMessage = ConnectionTools.GetStringRequest(playerSecondClient);
                    ConnectionTools.SendMessage(playerFirstClient, requestMessage);

                    string responseMessage = ConnectionTools.GetStringRequest(playerSecondClient);
                    ConnectionTools.SendMessage(playerFirstClient, responseMessage);

                    goingPlayer = playerSecondClient;
                }
                else
                {
                    string requestMessage = ConnectionTools.GetStringRequest(playerFirstClient);
                    ConnectionTools.SendMessage(playerSecondClient, requestMessage);

                    string responseMessage = ConnectionTools.GetStringRequest(playerFirstClient);
                    ConnectionTools.SendMessage(playerSecondClient, responseMessage);

                    goingPlayer = playerFirstClient;
                }

            }


            playerFirstClient.Close();
            playerSecondClient.Close();

            server.Stop();

            Logger.Log("SERVER STOPED");
            Console.ReadLine();
        }

        private static TcpClient AcceptClient(TcpListener server, string teame)
        {
            TcpClient player = server.AcceptTcpClient();
            Logger.Log($"player {teame} connected from { player.Client.RemoteEndPoint}");
            ConnectionTools.SendResponce(player, teame);

            return player;
        }
    }

   
}
