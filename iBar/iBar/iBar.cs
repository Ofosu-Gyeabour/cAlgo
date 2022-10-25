using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

#region Additional-Namespaces

using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

#endregion

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class iBar : Robot
    {
        /* this is the inside Bar price action robot */
        [Parameter("Data Source")]
        public DataSeries Price { get; set; }

        [Parameter("Trade Volume",DefaultValue = 10)]
        public int TradingVolume{get;set;}

        [Parameter("Max Trades",DefaultValue = 2)]
        public int MaxTrades{get;set;}
        
        [Parameter("Max Pips",DefaultValue = 20)]
        public int MaxPips{get;set;}   //determine stop value from max pips
        
        
        private Bar firstBar;
        private Bar secondBar;


        #region Pending-Order variables

        private double BuyStopOrder;
        private double SellStopOrder;
        private double StopLoss;
        private double ProfitTarget;


        #endregion

        protected override void OnStart()
        {
            //initialization 
            BuyStopOrder = 0d;
            SellStopOrder = 0d;
            StopLoss = 0d;
            ProfitTarget = 0d;
        }

        

        private void ClearPendingOrders()
        {
            //method is responsible for clearing all pending orders
            try
            {
                foreach (var order in PendingOrders)
                {
                    CancelPendingOrder(order);
                }
            } catch (Exception)
            {

            }
        }
        
        private void GetData()
        {
            //starting another thread to handle data-gathering
            
            //test conditions
            
                if ((secondBar.Low > firstBar.Low) && (secondBar.High < firstBar.High) && (DoBullishTest()))
                {
                    //bullish condition have been met. set pending order
                    BuyStopOrder = firstBar.High + 0.1 * (firstBar.High - firstBar.Low);
                    StopLoss = firstBar.High - 0.4 * (firstBar.High - firstBar.Low);
                    ProfitTarget = firstBar.High + 0.8 * (firstBar.High - firstBar.Low);

                    ClearPendingOrders();

                    PlaceLimitOrderAsync(TradeType.Buy, SymbolName, TradingVolume, BuyStopOrder, SymbolName, StopLoss, ProfitTarget);
                    //PlaceLimitOrder(TradeType.Buy, SymbolName, 10000, BuyStopOrder, SymbolName, StopLoss, ProfitTarget);

                }
                else if ((secondBar.Low > firstBar.Low) && (secondBar.High < firstBar.High) && (DoBearishTest()))
                {
                    //bearish condition have been met..setting pending order
                    SellStopOrder = firstBar.Low - 0.1 * (firstBar.High - firstBar.Low);
                    StopLoss = firstBar.Low + 0.4 * (firstBar.High - firstBar.Low);
                    ProfitTarget = firstBar.Low - 0.8 * (firstBar.High - firstBar.Low);

                    ClearPendingOrders();

                    PlaceLimitOrderAsync(TradeType.Sell, SymbolName, TradingVolume , SellStopOrder, SymbolName, StopLoss, ProfitTarget);
                    //PlaceLimitOrder(TradeType.Sell, SymbolName, 10000, SellStopOrder, SymbolName, StopLoss, ProfitTarget);

                }
            
        }

        private bool DoBullishTest()
        {
            //test the first bar for bullish conditions
            if (firstBar.Close > firstBar.Open)
            {
                return true;
            }
            else{return false;}
        }

        private bool DoBearishTest()
        {
            //test for bearish conditions
            if (firstBar.Open > firstBar.Close){
                return true;
            }
            else{return false;}
            
        }

        private void DoBuyAnalysis()
        {
            //method is responsible for buying

            return;
        }

        protected override void OnBar()
        {
            try
            {
                //get data on every onBar event
                this.GetData();
            } catch (Exception)
            {

            }
        }

        protected override void OnTick()
        {
            Print("ticked");
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }
    }
}
