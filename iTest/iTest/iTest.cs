using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class iTest : Robot
    {
        [Parameter(DefaultValue = "Hello world!")]
        public string Message { get; set; }


public enum enumABC{A,B,C};
[Parameter("Alphabet",Group = "WordCraft",DefaultValue = enumABC.C)]
public enumABC ABC{get;set;}

        protected override void OnStart()
        {
            // To learn more about cTrader Automate visit our Help Center:
            // https://help.ctrader.com/ctrader-automate

            Print(Message);
        }

        protected override void OnTick()
        {
            //testing the tick event
            Print("Tick is triggered");
        }

        protected override void OnStop()
        {
            // Handle cBot stop here
        }
    }
}