using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vindinium
{
    class Client
    {
        /**
         * Launch client.
         * @param args args[0] Private key
         * @param args args[1] [training|arena]
         * @param args args[2] number of turns
         * @param args args[3] HTTP URL of Vindinium server (optional)
         */
        static void Main(string[] args)
		{
			// grab some values or use defaults if nothing was passed
			string secretKey = args.Length >= 1 ? args [0] : "hk59lbjk";
			bool isTraingMode = (args.Length >= 2 && args[1] == "arena") ? false : true;
			uint turnAmount = args.Length >= 3 ? uint.Parse(args[2]) : 250;
            string serverURL = args.Length >= 4 ? args[3] : "http://vindinium.org";
			string map = null;

            //create the server stuff, when not in training mode, it doesnt matter
            //what you use as the number of turns
			//isTraingMode = false;
			ServerStuff serverStuff = new ServerStuff(secretKey, isTraingMode, turnAmount, serverURL, map);

            //create the random bot, replace this with your own bot
            //RandomBot bot = new RandomBot(serverStuff);
			StateMachineBot bot = new StateMachineBot (serverStuff);

            //now kick it all off by running the bot.
            bot.run();

            Console.Out.WriteLine("done");

            Console.Read();
        }
    }
}
