using ConnectionLibrary;
using ConnectionLibrary.Entity;
using ConnectionLibrary.Tools;
using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Client
{
    class Program
    {
        private const int RowCount = 10;
        private const int ColumnCount = 10;
        private const string OpponentField = "getopponentfield";
        private const string MyField = "getMyfield";


        static void Main(string[] args)
        {

            TcpClient client = ConnectionTools.Connect();

            if (client != null)
                Play(client);

            Console.ReadLine();
        }

        private static void Play(TcpClient client)
        {
            try
            {
                int currentStepNumber = 0;
                Random random = new Random();
                CellValue[,] firstPlayerField = new CellValue[RowCount, ColumnCount];
                CellValue[,] secondPlayerField = new CellValue[RowCount, ColumnCount];
                ClearField(firstPlayerField);
                ClearField(secondPlayerField);
                firstPlayerField = RandomField(firstPlayerField, random);
                secondPlayerField = RandomField(secondPlayerField, random);

                Console.OutputEncoding = Encoding.Unicode;

                const int FielsSize = 10;

                string team = ConnectionTools.GetResponce(client).Value;
                Console.WriteLine("Вы играете за " + team);

                if (team == ConstantData.PlayerChars.First)
                    Console.WriteLine("Ожидаем второго игрока");

                string gameStates = ConnectionTools.GetResponce(client).Value;
                bool isPlaying = true;

                GameStatus gameStatus = GameStatus.Play;

                while (isPlaying)
                {
                    Console.Clear();
                    Console.WriteLine("Вы играете за " + team);
                   

                    switch (gameStates)
                    {
                        case ConstantData.GameStates.Go:
                            
                            PrintField(client, team);
                            Step(client, FielsSize);
                            gameStatus = GetGameStatus(firstPlayerField,secondPlayerField);
                            ConnectionTools.SendResponce(client,ConstantData.GameStates.Go);
                            ConnectionTools.SendRequest(client,new Request() {Command=GetGameStatusString(gameStatus), Parameters=new string[] {GetFields(client) } });
                            gameStates = ConstantData.GameStates.Wait;
                            break;
                        case ConstantData.GameStates.Wait:
                            Console.WriteLine("Ждем ход противника");
                            break;
                    }

                    gameStates = ConnectionTools.GetResponce(client).Value;

                    if (ConnectionTools.GetResponce(client).Value == ConstantData.GameStates.End)
                        isPlaying = false;
                }
                currentStepNumber++;

                SetStepParameters(currentStepNumber, client, client,
                    out string currentValue, out TcpClient currentPlayer,
                    out string stateCrossAfter, out string stateZeroAfter);

                bool isStepEnd = false;

                while (isStepEnd == false)
                {
                    Request request = ConnectionTools.GetRequest(currentPlayer);

                    if (request.Command == ConstantData.Commands.Step)
                        Logger.Log($"STEP №{currentStepNumber} by {currentValue}: {request.Parameters[0]},{request.Parameters[1]}");

                    switch (request.Command)
                    {
                        case ConstantData.Commands.Step:
                            if (currentValue == "1")
                                ProcessCommandStep(currentPlayer, secondPlayerField, request.Parameters, currentValue);
                            else if (currentValue == "2")
                                ProcessCommandStep(currentPlayer, firstPlayerField, request.Parameters, currentValue);
                            break;
                        case ConstantData.Commands.EndStep:
                            isStepEnd = true;
                            ProcessCommandEndStep(firstPlayerField, secondPlayerField, client, client, stateCrossAfter, stateZeroAfter);
                            break;
                        case ConstantData.Commands.GetFields:
                            GetAllFields(firstPlayerField, secondPlayerField, currentValue, client, client);
                            break;
                    }
                }
                Console.Clear();
                Console.WriteLine("Ваше поле: ");
                Console.WriteLine(string.Join("Поле соперника:", GetFields(client)));

                string winner = ConnectionTools.GetResponce(client).Value;
                Console.WriteLine(winner);
            }
            catch (Exception exception)
            {
                Console.WriteLine("ERROR: " + exception.Message);
            }
        }

        private static void PrintField(TcpClient client, string team)
        {
            if (team == "1")
            {
                Console.WriteLine("Ваше поле: ");
                Console.WriteLine(string.Join("Поле соперника:", GetFields(client)));
            }
            else if (team == "2")
            {
                Console.WriteLine("Ваше поле: ");
                Console.WriteLine(string.Join("Поле соперника:", GetFields(client)));
            }
        }

        private static void ProcessCommandEndStep(CellValue[,] firstField, CellValue[,] secondField, TcpClient firstPlayer, TcpClient secondPlayer, string stateCrossAfter, string stateZeroAfter)
        {
            ConnectionTools.SendResponce(firstPlayer, stateCrossAfter);
            ConnectionTools.SendResponce(secondPlayer, stateZeroAfter);

            bool isEndGame = GetGameStatus(firstField, secondField) == GameStatus.Play;
            string endResult = isEndGame ? ConstantData.ResponceResults.Ok : ConstantData.GameStates.End;

            ConnectionTools.SendResponce(firstPlayer, endResult);
            ConnectionTools.SendResponce(secondPlayer, endResult);
        }

        private static void ProcessCommandGetField(TcpClient player, CellValue[,] field)
        {
            ConnectionTools.SendResponce(player, GetField(field, MyField));
        }

        private static void SetStepParameters(int currentStep, TcpClient playerFirstClient, TcpClient playerSecondClient,
          out string currentValue, out TcpClient currentPlayer, out string stateFirstAfter, out string stateSecondAfter)
        {
            if (currentStep % 2 == 0)
            {
                currentValue = ConstantData.PlayerChars.Second;
                currentPlayer = playerSecondClient;
                stateFirstAfter = ConstantData.GameStates.Go;
                stateSecondAfter = ConstantData.GameStates.Wait;
            }
            else
            {
                currentValue = ConstantData.PlayerChars.First;
                currentPlayer = playerFirstClient;
                stateSecondAfter = ConstantData.GameStates.Go;
                stateFirstAfter = ConstantData.GameStates.Wait;
            }
        }

        private static void Step(TcpClient client, int fielsSize)
        {
            do
            {
                string i = GetClumpValue("i", 1, fielsSize);
                string j = GetClumpValue("j", 1, fielsSize);

                Request request = new Request() { Command = ConstantData.Commands.Step, Parameters = new string[] { i, j } };
                ConnectionTools.SendRequest(client, request);
            } while (ConnectionTools.GetResponce(client).Result == ConstantData.ResponceResults.Error);
        }

        private static string GetClumpValue(string valueName, int start, int end)
        {
            Console.WriteLine($"Введите {valueName} от {start} до {end}");
            return Console.ReadLine();
        }

        private static string DeserializeField(string data)
        {
            string[] lines = data.Split(':', '/');
            return string.Join("\n", lines);
        }

        private static string GetFields(TcpClient client)
        {
            Request request = new Request() { Command = ConstantData.Commands.GetFields };
            ConnectionTools.SendRequest(client, request);

            return DeserializeField(ConnectionTools.GetResponce(client).Value);
        }

        public static void GetAllFields(CellValue[,] firstPlayerField, CellValue[,] secondPlayerField, string value,
           TcpClient playerFirst, TcpClient playerSecond)
        {
            string usersFields;

            if (value == "1")
            {
                usersFields = GetField(firstPlayerField, MyField) + GetField(secondPlayerField, OpponentField);

                ConnectionTools.SendResponce(playerFirst, usersFields);
            }
            else if (value == "2")
            {
                usersFields = GetField(secondPlayerField, MyField) + GetField(firstPlayerField, OpponentField);

                ConnectionTools.SendResponce(playerSecond, usersFields);
            }
        }

        private static string GetGameStatusString(GameStatus status)
        {
            switch (status)
            {
                case GameStatus.Play:
                    return ConstantData.GameStatus.Play;
                case GameStatus.WinFirst:
                    return ConstantData.GameStatus.WinFirst;
                case GameStatus.WinSecond:
                    return ConstantData.GameStatus.WinSecond;
            }

            return string.Empty;
        }

        

        private static string GetField(CellValue[,] field, string value)
        {
            string textField = string.Empty;

            if (value == MyField)
            {
                for (int i = 0; i < RowCount; i++)
                {
                    for (int j = 0; j < ColumnCount; j++)
                    {
                        switch (field[i, j])
                        {
                            case CellValue.Empty:
                                textField += "-";
                                break;
                            case CellValue.Ship:
                                textField += ConstantData.PlayerChars.Ship;
                                break;
                            case CellValue.First:
                                textField += ConstantData.PlayerChars.First;
                                break;
                            case CellValue.FirstHit:
                                textField += ConstantData.PlayerChars.HitFirst;
                                break;
                            case CellValue.Second:
                                textField += ConstantData.PlayerChars.Second;
                                break;
                            case CellValue.SecondHit:
                                textField += ConstantData.PlayerChars.HitSecond;
                                break;
                        }
                    }

                    textField += ":";
                }

                textField += "/";
            }
            else if (value == OpponentField)
            {
                for (int i = 0; i < RowCount; i++)
                {
                    for (int j = 0; j < ColumnCount; j++)
                    {
                        switch (field[i, j])
                        {
                            case CellValue.First:
                                textField += ConstantData.PlayerChars.First;
                                break;
                            case CellValue.FirstHit:
                                textField += ConstantData.PlayerChars.HitFirst;
                                break;
                            case CellValue.Second:
                                textField += ConstantData.PlayerChars.Second;
                                break;
                            case CellValue.SecondHit:
                                textField += ConstantData.PlayerChars.HitSecond;
                                break;
                            case CellValue.Ship:
                            case CellValue.Empty:
                                textField += "-";
                                break;
                        }
                    }

                    textField += ":";
                }
            }

            return textField;
        }

        private static void ProcessCommandStep(TcpClient player, CellValue[,] field, string[] coordinates, string value)
        {
            bool stepResult;

            int.TryParse(coordinates[0], out int i);
            int.TryParse(coordinates[1], out int j);

            stepResult = TryMakeStep(field, i, j, value == ConstantData.PlayerChars.First ? CellValue.First : CellValue.Second);

            string responce = stepResult ? value : string.Empty;
            ConnectionTools.SendResponce(player, responce);
        }

        private static bool TryMakeStep(CellValue[,] field, int i, int j, CellValue value)
        {
            if ((i > RowCount || j > ColumnCount) || (i <= 0 || j <= 0) || field[i - 1, j - 1]
                == CellValue.First || field[i - 1, j - 1] == CellValue.Second)
                return false;

            if (field[i - 1, j - 1] != CellValue.Ship)
                field[i - 1, j - 1] = value;
            else if (field[i - 1, j - 1] == CellValue.Ship)
                if (value == CellValue.First)
                    field[i - 1, j - 1] = CellValue.FirstHit;
                else if (value == CellValue.Second)
                    field[i - 1, j - 1] = CellValue.SecondHit;

            return true;
        }

        private static GameStatus GetGameStatus(CellValue[,] firstPlayerField, CellValue[,] secondPlayerField)
        {
            if (CheckWinCondition(firstPlayerField))
                return GameStatus.WinSecond;

            if (CheckWinCondition(secondPlayerField))
                return GameStatus.WinFirst;

            return GameStatus.Play;
        }

        private static bool CheckWinCondition(CellValue[,] field)
        {
            int shipcount = 0;

            for (int i = 0; i < field.GetLength(0); i++)
            {
                for (int j = 0; j < field.GetLength(1); j++)
                {
                    if (field[i, j] == CellValue.Ship)
                    {
                        shipcount++;
                    }
                }
            }

            if (shipcount == 0)
                return true;

            return false;
        }

        

        private static void ClearField(CellValue[,] field)
        {
            for (int i = 0; i < RowCount; i++)
                for (int j = 0; j < ColumnCount; j++)
                    field[i, j] = CellValue.Empty;
        }

        private static CellValue[,] RandomField(CellValue[,] field, Random random, int shipCount = 5)
        {
            //Console.OutputEncoding = Encoding.GetEncoding(866);

            for (int i = 0; i < shipCount; i++)
            {
                int iParameter = random.Next(0, RowCount - 1);
                int jParameter = random.Next(0, ColumnCount - 1);

                if (field[iParameter, jParameter] != CellValue.Ship)
                {
                    field[iParameter, jParameter] = CellValue.Ship;
                }
                else if (field[iParameter, jParameter] == CellValue.Ship)
                {
                    i--;
                }
            }

            return field;
        }

        public enum CellValue
        {
            Ship = 'X',
            Empty = '.',
            First = '1',
            FirstHit = '①',
            Second = '2',
            SecondHit = '②'
        }

        public enum GameStatus
        {
            Play = -1,
            Draw = 0,
            WinFirst = 1,
            WinSecond = 2
        }
    }
}
