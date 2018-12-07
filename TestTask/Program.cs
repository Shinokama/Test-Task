using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;

namespace TestTask
{
    //Основной класс
    class Program
    {

        static Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //Режимы
        static bool report;
        static bool log;
        static void Main(string[] args)
        {
            //Определение порта. По умолчанию 70.
            int port;
            try
            {
                port = Convert.ToInt32(args[0]);
            }
            catch
            {
                Console.WriteLine("Port option is missing or invalid.");
                port = 70;
            }
            Console.WriteLine("Server is starting on port " + port);

            //режимы
            report = false;
            log = false;

            //Включение сокета
            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                socket.Listen(0);
            }
            catch
            {
                //Выход с ошибкой, если порт занят
                Console.Write("Error! Port is busy\nPress any key to exit...");
                Console.ReadKey();
                Environment.Exit(10);
            }


            //Вечный цикл. При появлении соединения, соединение передается в метод NetHandlerThread
            while (true)
            {
                //Отправляем пользователей по разным потокам. Реализацию подсмотрел на хабре.
                ThreadPool.QueueUserWorkItem(new WaitCallback(NetHandlerThread), socket.Accept());
            }
        }

        //Прием/передача данных. Вызывается при установке соединения с клиентом.
        static void NetHandlerThread(Object session)
        {
            Socket client = (Socket)session;
            //На случай разрыва сессии пользователем.
            try
            {
                //Без ответной отправки телнет клиент не отображает символы в консоли. Фикс.
                client.Send(Encoding.ASCII.GetBytes("\r"));
                String command = "";
                //Вечный цикл. До выхода или разрыва соединения.
                while (client.Connected)
                {
                    //Ограничение, сколько байт можен быть обработано за раз. При необходимости можно увеличить.
                    byte[] buffer = new byte[1];
                    client.Receive(buffer);
                    command += Encoding.ASCII.GetString(buffer);

                    //Определение, что был ввод.
                    if (command.Contains("\r\n"))
                    {
                        if (command.IndexOf("\r\n") > 0)
                        {
                            client.Send(Encoding.ASCII.GetBytes(execute_command(command.Substring(0, command.IndexOf("\r\n")), client)));
                            
                        }

                        //Удаление выполненной команды из буфера, или обнуление всего буфера, если она там была одна.
                        //По хорошему тут бы применить RegEx, но так было быстрее, а особых требований не было.
                        command = (command.IndexOf("\r\n") + 4 < command.Length) ? command.Substring(command.IndexOf("\r\n") + 4) : "";
                    }
                }
            }
            catch
            {
                //В любой непонятной ситуации - гаси соединение
                client.Close();
            }
        }
        
        //Обработка команд. Сокет передается для возможности его закрытия из метода.
        static String execute_command(String command, Socket socket)
        {
            //Обработка режима report
            String result = "";
            if (report)
            {
                result = command + "\r\n";
            }

            //Обработка режима логгировния
            if (log)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("./log.txt", true, Encoding.Default))
                    {
                        sw.WriteLine(command);
                    }
                }
                catch
                {
                    //Отключение логгирования в случае ошибки записи. Уведомление пользователя.
                    log = false;
                    result += "log write error, log mode disabled";
                }
            }

            //Обработка команд с аргументами
            if (command.Contains(":") && ((1 < command.Split(':').Length) && (command.Split(':').Length < 3)))
            {
                String param = command.Split(':')[0];
                String value = command.Split(':')[1];
                switch (param)
                {
                    case "report":
                        switch (value)
                        {
                            case "on":
                                report = true;
                                break;
                            case "off":
                                report = false;
                                break;
                            default:
                                return result;

                        }
                        break;
                    case "log":
                        switch (value)
                        {
                            case "on":
                                log = true;
                                break;
                            case "off":
                                log = false;
                                break;
                            default:
                                return result;

                        }
                        break;
                    default:
                        break;
                }
            }
            //Обработка команд без аргументов
            else
            {
                switch (command)
                {
                    case "time":
                        result += Convert.ToString(DateTime.Now) + "\r\n";
                        break;

                    //Добавил от себя exit для выхода из сессии и shutdown для выключения сервера.
                    case "exit":
                        socket.Close();
                        break;
                    case "shutdown":
                        Environment.Exit(0);
                        break; //вообще не уверен, что до сюда когда - либо дойдет, но VS требовал.

                }
            }
            return result;
        }
    }
}
