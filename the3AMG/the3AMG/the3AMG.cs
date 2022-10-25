using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class the3AMG : Robot
    {

        #region Parameters

        private ExponentialMovingAverage _emaFast;
        private ExponentialMovingAverage _emaSlow;
        private ExponentialMovingAverage _emaMedian;

        [Parameter("Fast Period", DefaultValue = 15)]
        public int fastPeriod { get; set; }

        [Parameter("Median Period", DefaultValue = 25)]
        public int medianPeriod { get; set; }

        [Parameter("Slow Periods", DefaultValue = 50)]
        public int slowPeriod { get; set; }

        [Parameter("Periods to Consider", DefaultValue = 10)]
        public int periodsToConsider { get; set; }

        [Parameter("Data Source")]
        public DataSeries Price { get; set; }

        [Parameter("Trade Start Time(GMT)", DefaultValue = 4)]
        public int startingTime { get; set; }
        //time from which to enter the market. Default is 04:00GMT
        [Parameter("Trade End Time(GMT)", DefaultValue = 23)]
        public int endingTime { get; set; }
        //threshold time for not entering the market. Default is 21:00GMT

        [Parameter("Opened Pos Symbol Count", DefaultValue = 2)]
        public int PositionCount { get; set; }

        [Parameter("Lots size", DefaultValue = 1000, MinValue = 0)]
        public int lotSize { get; set; }

        [Parameter("Stop Loss", DefaultValue = 40)]
        public int StopLoss { get; set; }

        [Parameter("Take Profit", DefaultValue = 10)]
        public int TakeProfit { get; set; }

        private Dictionary<int, TradeResult> dict;

        #endregion

        protected override void OnStart()
        {
            //between 9pm and 4am, do NOT enter the market
            //use some of the variables used in the EA for MT4
            try
            {
                if (isValidTradingDay() && isValidTradingTimePeriod())
                {
                    _emaFast = Indicators.ExponentialMovingAverage(Price, fastPeriod);
                    _emaMedian = Indicators.ExponentialMovingAverage(Price, medianPeriod);
                    _emaSlow = Indicators.ExponentialMovingAverage(Price, slowPeriod);
                }
            } catch
            {
            }
        }

        protected override void OnBar()
        {

            var longPosition = Positions.Find(string.Empty, SymbolName, TradeType.Buy);
            var shortPosition = Positions.Find(string.Empty, SymbolName, TradeType.Sell);

            try
            {

                if (isValidTradingDay() && isValidTradingTimePeriod() && isValidPositionCount())
                {
                    TradeResult result = null;
                    string statusMsg = string.Empty;

                    if (BuyCondition())
                    {
                        statusMsg = "Position of BUY entry price is {0}";
                        //ManageTradingPositions(Symbol.Name);
                        //CloseTradetypePositions(TradeType.Sell);
                        result = ExecuteMarketOrder(TradeType.Buy, SymbolName, this.lotSize, phrasePositionLabel(SymbolName), null, null);
                    }
                    else if (SellCondition())
                    {
                        statusMsg = "Position of SELL entry price is {0}";
                        //CloseTradetypePositions(TradeType.Buy);
                        //ManageTradingPositions(Symbol.Name);
                        result = ExecuteMarketOrder(TradeType.Sell, SymbolName, this.lotSize, phrasePositionLabel(SymbolName), null, null);
                    }

                    if (result.IsSuccessful)
                    {
                        var position = result.Position;
                        dict.Add(position.Id, result);

                        Print(statusMsg, position.EntryPrice);
                    }
                }
            } catch
            {

            }
        }


        private string phrasePositionLabel(string tradeSymbol)
        {
            //method is responsible for phrasing the label of a position
            var _timeString = new DateTime().ToString("HH:mm:ss");
            var result = string.Format("{0}/{1}", tradeSymbol, _timeString);

            return result;
        }

        private void ManageTradingPositions(string nameOfSymbol)
        {
            /*
                method manages opened Positions
                in order to allow positions to make as much money without hitting take profit (TP)
                if the position is positive, and it is still making good money after 2H, keep it open
                if the position is positive, and the trend is changing, close the position
                
            */

            foreach (var d in this.dict)
            {
                if (d.Value.Position.SymbolName == nameOfSymbol)
                {
                    if (d.Value.Position.Pips < (double)this.lotSize)
                    {

                    }
                }
                var _entryT = d.Value.Position.EntryTime;
                var _entryP = d.Value.Position.EntryPrice;

                //finding difference in time

            }
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here

        }

        #region Private Methods


        private bool isValidTradingTimePeriod()
        {
            //determines if we are in the valid time for trading
            bool bln = false;
            TimeSpan endTime = new TimeSpan(this.endingTime, 0, 0);
            TimeSpan startTime = new TimeSpan(this.startingTime, 0, 0);
            TimeSpan now = DateTime.Now.TimeOfDay;

            if ((now > startTime) && (now < endTime))
            {
                //analyze market and perform trading routines
                return (bln = true);
            }
            else
            {
                return bln;
            }
        }

        private bool isValidTradingDay()
        {
            //determines if the day is a valid trading day
            bool bln = false;
            var day = DateTime.Now.DayOfWeek;
            if ((day != DayOfWeek.Saturday) || (day != DayOfWeek.Sunday))
            {
                //it is a weekday: analyze market and perform trading routines
                return (bln = true);
            }
            else
            {
                return bln;
            }
        }

        private bool isValidPositionCount()
        {
            //determines if the number of opened positions is within range
            bool bln = false;
            int pos = Positions.Count;
            if (pos < this.PositionCount)
            {
                return bln = true;
            }
            else
            {
                return bln;
            }

        }

        private bool BuyCondition()
        {
            int index = Bars.OpenTimes.Count - 2;
            bool bln = false;
            if (_emaFast.Result.HasCrossedAbove(_emaMedian.Result, 0) && (_emaMedian.Result.HasCrossedAbove(_emaSlow.Result, 0)))
            {
                Print("BUY CONDITION SATISFIED::>Fast EMA = {0}, Median EMA = {1}, Slow EMA = {2}", Math.Round(_emaFast.Result.LastValue, 4), Math.Round(_emaMedian.Result.LastValue, 4), Math.Round(_emaSlow.Result.LastValue, 4));
            }
            //if ((_emaFast.Result[index] > _emaSlow.Result[index]) && (_emaFast.Result[index] > _emaMedian.Result[index]))
            //if ((_emaFast.Result[index] < _emaSlow.Result[index]) && (_emaFast.Result[index] < _emaMedian.Result[index]))
            if (_emaFast.Result.HasCrossedAbove(_emaMedian.Result, 0) && (_emaMedian.Result.HasCrossedAbove(_emaSlow.Result, 0)))
            {
                bln = true;
            }

            return bln;
        }

        private bool SellCondition()
        {
            int index = Bars.OpenTimes.Count - 2;
            bool bln = false;

            if (_emaFast.Result.HasCrossedBelow(_emaMedian.Result, 0) && (_emaMedian.Result.HasCrossedBelow(_emaSlow.Result, 0)))
            {
                Print("SELL CONDITION SATISFIED::>Fast EMA = {0}, Median EMA = {1}, Slow EMA = {2}", Math.Round(_emaFast.Result.LastValue, 4), Math.Round(_emaMedian.Result.LastValue, 4), Math.Round(_emaSlow.Result.LastValue, 4));
            }
            //if ((_emaFast.Result[index] < _emaSlow.Result[index]) && (_emaFast.Result[index] < _emaMedian.Result[index]))
            //if ((_emaFast.Result[index] > _emaSlow.Result[index]) && (_emaFast.Result[index] > _emaMedian.Result[index]))
            if (_emaFast.Result.HasCrossedBelow(_emaMedian.Result, 0) && (_emaMedian.Result.HasCrossedBelow(_emaSlow.Result, 0)))
            {
                bln = true;
            }


            return bln;
        }

        private void ExecuteOrder(double quantity, TradeType tradeType)
        {
            var volumeInUnits = Symbol.QuantityToVolumeInUnits(quantity);
            var result = ExecuteMarketOrder(tradeType, SymbolName, volumeInUnits);

            if (result.Error == ErrorCode.NoMoney)
                Stop();
        }

        private void ExecuteOrderAssync(double quantity, TradeType tradeType, string status)
        {
            var volumeInUnits = Symbol.QuantityToVolumeInUnits(quantity);
            TradeOperation operation = ExecuteMarketOrderAsync(TradeType.Buy, SymbolName, this.lotSize);

            if (operation.IsExecuting)
            {
                Print("Operation Is Executing");
            }
        }

        #endregion


    }


}
