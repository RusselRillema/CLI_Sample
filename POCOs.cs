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
        [CliTableFormat(Header = "ID", LeadingCharacters = 3, TrailingCharacters = 3)]
        public Guid Id { get; set; } = Guid.NewGuid();
        [CliTableFormat(Header = "Acc. Name")]
        public string Name { get; set; }
        public Exchange Exchange { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }
    public class Instrument
    {
        public Exchange Exchange { get; set; }
        [CliTableFormat(Header = "Inst. Name")]
        public string Name { get; set; }
        public string Symbol { get; set; }
        [CliTableFormat(Header = "Inst. Kind")]
        public InstrumentKind InstrumentKind { get; set; }
        public override string ToString()
        {
            return Symbol;
        }
    }

    public class Order
    {
        [CliTableFormat(Header = "ID", LeadingCharacters = 3, TrailingCharacters = 3)]
        public Guid ID { get; set; } = Guid.NewGuid();
        public Account Account { get; set; }
        [CliTableFormat(Header = "Symbol")]
        public Instrument Instrument { get; set; }
        [CliTableFormat(Header = "Side")]
        public Side Side { get; set; }
        public decimal Qty { get; set; }
        public decimal Price { get; set; }
        public OrderState State { get; set; }
        [CliTableFormat(Header = "Open")]
        public bool IsOpen 
        { 
            get
            {
                switch (State)
                {
                    case OrderState.Open:
                        return true;
                    case OrderState.Filled:
                    case OrderState.Canceled:
                        return false;
                    default:
                        throw new Exception($"Unknown order state {State}");
                }
            }
        }
    }

    public enum FilterType
    {
        None,
        Equals,
        Contains,
        StartsWith,
        EndsWith,
    }

    public static class SampleData
    {
        public static List<Account> FindAccounts(FilterType filterType, string idOrName)
        {

            switch (filterType)
            {
                case FilterType.None:
                    return _accounts.ToList();
                case FilterType.Equals:
                    if (Guid.TryParse(idOrName, out Guid id))
                        return _accounts.Where(x => x.Id == id).ToList();
                    else
                        return _accounts.Where(x => x.Name == idOrName).ToList();
                case FilterType.Contains:
                    return _accounts.Where(x => x.Name.Contains(idOrName)).Concat(_accounts.Where(x => x.Id.ToString().Contains(idOrName))).ToList();
                case FilterType.StartsWith:
                    return _accounts.Where(x => x.Name.StartsWith(idOrName)).Concat(_accounts.Where(x => x.Id.ToString().StartsWith(idOrName))).ToList();
                case FilterType.EndsWith:
                    return _accounts.Where(x => x.Name.EndsWith(idOrName)).Concat(_accounts.Where(x => x.Id.ToString().EndsWith(idOrName))).ToList();
                default:
                    throw new Exception($"Unknown filter type {filterType}");
            }
        }

        public static Account FindAccount(string idOrName)
        {
            if (Guid.TryParse(idOrName, out Guid id))
            {
                var matchesByGuids = _accounts.Where(x => x.Id == id);
                if (matchesByGuids.Count() > 1)
                    throw new Exception($"More than 1 account with matching Id of {id}");
                else if (matchesByGuids.Count() == 1)
                    return matchesByGuids.Single();
            }

            var matchesByName = _accounts.Where(x => x.Name.ToLower() == idOrName.ToLower());
            if (matchesByName.Count() == 0)
                throw new Exception($"No accounts match on Id or Name to {idOrName}");
            else if (matchesByName.Count() > 1)
                throw new Exception($"More than 1 account with matching Name of {idOrName}");

            return matchesByName.Single();
        }

        public static bool TryFindAccount(string idOrName, out Account? account)
        {
            try
            {
                account = FindAccount(idOrName);
                return true;
            }
            catch (Exception) 
            {
                account = null;
                return false;
            }
        }

        public static List<Instrument> FindInstruments(FilterType filterType, Account account, string symbol) 
        {
            switch (filterType)
            {
                case FilterType.None:
                    return _instruments.Where(x => x.Exchange == account.Exchange).ToList();
                case FilterType.Equals:
                    return _instruments.Where(x => x.Exchange == account.Exchange && x.Symbol.ToLower() == symbol.ToLower()).ToList();
                case FilterType.Contains:
                    return _instruments.Where(x => x.Exchange == account.Exchange && x.Symbol.ToLower().Contains(symbol.ToLower())).ToList();
                    throw new Exception($"Unknown filter type {filterType}");
                case FilterType.StartsWith:
                    return _instruments.Where(x => x.Exchange == account.Exchange && x.Symbol.ToLower().StartsWith(symbol.ToLower())).ToList();
                case FilterType.EndsWith:
                    return _instruments.Where(x => x.Exchange == account.Exchange && x.Symbol.ToLower().EndsWith(symbol.ToLower())).ToList();
                default:
                    throw new Exception($"Unknown filter type {filterType}");
            }
        }

        public static Instrument FindInstrument(Account account, string symbol)
        {
            var matches = _instruments.Where(x=>x.Exchange == account.Exchange && x.Symbol.ToLower() == symbol.ToLower());
            if (matches.Count() == 0)
                throw new Exception($"No instrument on exchange {account.Exchange} with symbol {symbol}");
            else if (matches.Count() > 1)
                throw new Exception($"More than 1 instrument on exchange {account.Exchange} with symbol {symbol}");

            return matches.Single();

        }

        public static bool TryFindInstrument(Account account, string symbol, out Instrument? instrument)
        {
            try
            {
                instrument = FindInstrument(account, symbol);
                return true;
            }
            catch (Exception)
            {
                instrument = null;
                return false;
            }
        }

        internal static List<Order> FindOrders(OrdersFilter filterType, string filter)
        {
            List<Order> res = new();
            foreach (var item in Orders)
            {
                switch (filterType)
                {
                    case OrdersFilter.Open:
                        if (item.IsOpen)
                            res.Add(item);
                        break;
                    case OrdersFilter.Completed:
                        if (!item.IsOpen)
                            res.Add(item);
                        break;
                    case OrdersFilter.All:
                        res.Add(item);
                        break;
                    case OrdersFilter.State:
                        if (item.State.ToString() == filter)
                            res.Add(item);
                        break;
                    case OrdersFilter.Account:
                        if (item.Account.Name == filter)
                            res.Add(item);
                        break;
                    case OrdersFilter.Instrument:
                        if (item.Instrument.Symbol == filter)
                            res.Add(item);
                        break;
                    default:
                        break;
                }
            }
            return res;
        }

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
    }
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
