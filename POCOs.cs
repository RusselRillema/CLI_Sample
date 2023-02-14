using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CLI_Sample
{
    public enum Exchange
    {
        Binance,
        Bybit,
        BitMEX,
        OKX
    }
    public class Account
    {
        public string Name { get; set; }
        public Exchange Exchange { get; set; }
    }
    public class Instrument
    {
        public Exchange Exchange { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public InstrumentKind InstrumentKind { get; set; }
    }

    public class Order
    {
        public Guid ID { get; set; } = Guid.NewGuid();
        public Account Account { get; set; }
        public Instrument Instrument { get; set; }
        public Side Side { get; set; }
        public decimal Qty { get; set; }
        public decimal Price { get; set; }
        public OrderState State { get; set; }
    }

    public static class SampleData
    {

        private static List<Account> _accounts = new()
        {
            new() {Name = "Bin1", Exchange = Exchange.Binance},
            new() {Name = "Bin2", Exchange = Exchange.Binance},
            new() {Name = "Bit1", Exchange = Exchange.BitMEX},
        };

        public static List<Account> Accounts
        {
            get
            {
                return _accounts;
            }
        }

        private static List<Instrument> _instruments = new()
        {
            new() {Symbol="BTCUSD", Name = "BCTUSD Perp", Exchange = Exchange.Binance},
            new() {Symbol="ETHUSD", Name = "ETHUSD Perp", Exchange = Exchange.Binance},
            new() {Symbol="ETHUSDC", Name = "ETHUSDC Perp", Exchange = Exchange.Binance},
            new() {Symbol="BTCUSD", Name = "BCTUSD Perp", Exchange = Exchange.BitMEX},
            new() {Symbol="BTCUSDT", Name = "BCTUSDT Perp", Exchange = Exchange.BitMEX},
            new() {Symbol="ETHUSDT", Name = "ETHUSDT Perp", Exchange = Exchange.BitMEX},
        };

        public static List<Instrument> Instruments
        {
            get
            {
                return _instruments;
            }
        }

        public static List<Order> Orders { get; } = new();
    };

    public enum InstrumentKind
    {
        None,
        Spot,
        Margin,
        Future,
        Perp,
        Option,
        Move,
        QuantoPerp,
        QuantoFuture
    }

    public enum Side
    {
        Buy,
        Sell
    }

    public enum OrderState
    {
        Open,
        Filled,
        Canceled,
    }
}
