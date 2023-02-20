using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CLI_Sample
{
    public class DuplicateResultException : Exception
    {
        public DuplicateResultException(string message, ICollection duplicates) : base(message)
        {
            Duplicates = duplicates;
        }
    
        public ICollection Duplicates { get; }
    }

    public enum Exchange
    {
        Binance,
        Bybit,
        BitMEX,
        OKX
    }
    public class Account
    {
        [CliTablePropertyFormat(Header = "ID", LeadingCharacters = 3, TrailingCharacters = 3, MaxWidth = 9)]
        public Guid Id { get; set; } = Guid.NewGuid();
        [CliTablePropertyFormat(Header = "Acc. Name")]
        public string Name { get; set; } = string.Empty;
        public Exchange Exchange { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }
    public class Instrument
    {
        public Exchange Exchange { get; set; }
        [CliTablePropertyFormat(Header = "Inst. Name")]
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; }
        [CliTablePropertyFormat(Header = "Inst. Kind")]
        public InstrumentKind InstrumentKind { get; set; }

        public string SW_Symbol => $"{Symbol}_{InstrumentKind}";

        public override string ToString()
        {
            return Symbol;
        }
    }

    public class InstrumentInfo
    {
        public Instrument Instrument { get; set; }
        public Exchange Exchange => Instrument.Exchange;
        public decimal BestBid { get; set; }
        public decimal BestOffer { get; set; }
        public decimal Spread => Math.Round(BestOffer - BestBid, 2);
        public decimal LastPrice { get; set; }
    }


    public class Order
    {
        [CliTablePropertyFormat(Header = "ID", LeadingCharacters = 3, TrailingCharacters = 3, MaxWidth = 9)]
        public Guid ID { get; set; } = Guid.NewGuid();
        public Account Account { get; set; }
        [CliTablePropertyFormat(Header = "Symbol")]
        public Instrument Instrument { get; set; }
        [CliTablePropertyFormat(Header = "Side")]
        public Side Side { get; set; }
        public decimal Qty { get; set; }
        public decimal Price { get; set; }
        public OrderState State { get; set; }
        [CliTablePropertyFormat(Header = "Open")]
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
                        return _accounts.Where(x => x.Name.ToLower() == idOrName).ToList();
                case FilterType.Contains:
                    return _accounts.Where(x => x.Name.ToLower().Contains(idOrName)).Concat(_accounts.Where(x => x.Id.ToString().Contains(idOrName))).ToList();
                case FilterType.StartsWith:
                    return _accounts.Where(x => x.Name.ToLower().StartsWith(idOrName)).Concat(_accounts.Where(x => x.Id.ToString().StartsWith(idOrName))).ToList();
                case FilterType.EndsWith:
                    return _accounts.Where(x => x.Name.ToLower().EndsWith(idOrName)).Concat(_accounts.Where(x => x.Id.ToString().EndsWith(idOrName))).ToList();
                default:
                    throw new Exception($"Unknown filter type {filterType}");
            }
        }

        public static Account FindAccount(string idOrName)
        {
            if (Guid.TryParse(idOrName, out Guid id))
            {
                var matchesByGuids = _accounts.Where(x => x.Id == id).ToList();
                if (matchesByGuids.Count() > 1)
                    throw new DuplicateResultException($"More than 1 account with matching Id of {id}", matchesByGuids);
                else if (matchesByGuids.Count() == 1)
                    return matchesByGuids.Single();
            }

            var matchesByName = FindAccounts(FilterType.Equals, idOrName);// _accounts.Where(x => x.Name.ToLower() == idOrName.ToLower()).ToList();
            if (matchesByName.Count() == 0)
                throw new Exception($"No accounts match on Id or Name to {idOrName}");
            else if (matchesByName.Count() > 1)
                throw new DuplicateResultException($"More than 1 account with Name {idOrName}. Use ID instead", matchesByName);

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
            var res = FindInstrumentsBySW_Symbol(filterType, account, symbol);

            if (res.Count == 0)
                res = FindInstrumentsBySymbol(filterType, account, symbol);

            return res;
        }

        private static List<Instrument> FindInstrumentsBySW_Symbol(FilterType filterType, Account account, string symbol)
        {
            switch (filterType)
            {
                case FilterType.None:
                    return _instruments.Where(x => x.Exchange == account.Exchange).ToList();
                case FilterType.Equals:
                    return _instruments.Where(x => x.Exchange == account.Exchange && x.SW_Symbol.ToLower() == symbol.ToLower()).ToList();
                case FilterType.Contains:
                    return _instruments.Where(x => x.Exchange == account.Exchange && x.SW_Symbol.ToLower().Contains(symbol.ToLower())).ToList();
                    throw new Exception($"Unknown filter type {filterType}");
                case FilterType.StartsWith:
                    return _instruments.Where(x => x.Exchange == account.Exchange && x.SW_Symbol.ToLower().StartsWith(symbol.ToLower())).ToList();
                case FilterType.EndsWith:
                    return _instruments.Where(x => x.Exchange == account.Exchange && x.SW_Symbol.ToLower().EndsWith(symbol.ToLower())).ToList();
                default:
                    throw new Exception($"Unknown filter type {filterType}");
            }
        }

        private static List<Instrument> FindInstrumentsBySymbol(FilterType filterType, Account account, string symbol)
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
            var matches = FindInstruments(FilterType.Equals, account, symbol);// _instruments.Where(x=>x.Exchange == account.Exchange && x.Symbol.ToLower() == symbol.ToLower()).ToList();
            if (matches.Count() == 0)
                throw new Exception($"No instrument on exchange {account.Exchange} with symbol {symbol}");
            else if (matches.Count() > 1)
                throw new DuplicateResultException($"More than 1 instrument on exchange {account.Exchange} with symbol {symbol}. Use SW_Symbol instead", matches);

            return matches.Single();

        }

        public static bool TryFindInstrument(Account account, string symbol, out Instrument? instrument)
        {
            try
            {
                instrument = FindInstrument(account, symbol);
                return true;
            }
            catch (DuplicateResultException dex)
            {
                throw;
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
            new() {Symbol="BTCUSD", Name = "BCTUSD Perp", Exchange = Exchange.Binance, InstrumentKind = InstrumentKind.Perp},
            new() {Symbol="BTCUSD", Name = "BCTUSD Future", Exchange = Exchange.Binance, InstrumentKind = InstrumentKind.Future},
            new() {Symbol="ETHUSD", Name = "ETHUSD Perp", Exchange = Exchange.Binance, InstrumentKind = InstrumentKind.Perp},
            new() {Symbol="ETHUSDC", Name = "ETHUSDC Perp", Exchange = Exchange.Binance, InstrumentKind = InstrumentKind.Perp},
            new() {Symbol="BTCUSD", Name = "BCTUSD Perp", Exchange = Exchange.BitMEX, InstrumentKind = InstrumentKind.Perp},
            new() {Symbol="BTCUSDT", Name = "BCTUSDT Perp", Exchange = Exchange.BitMEX, InstrumentKind = InstrumentKind.Perp},
            new() {Symbol="ETHUSDT", Name = "ETHUSDT Perp", Exchange = Exchange.BitMEX, InstrumentKind = InstrumentKind.Perp},
            new() {Symbol="ETHUSDT", Name = "ETHUSDT Future", Exchange = Exchange.BitMEX, InstrumentKind = InstrumentKind.Future},
        };

        public static List<Instrument> Instruments
        {
            get
            {
                return _instruments;
            }
        }

        private static List<Order> _orders { get; } = new();

        public static IReadOnlyList<Order> Orders { get => _orders; }

        public static async Task AddOrder(Order order)
        {
            _orders.Add(order);
            await Task.Run(() => { Thread.Sleep(3000); });
        }

        public static async Task<int> CancelOrders(IEnumerable<Order> orders)
        {
            int affectedOrders = 0;
            foreach (var item in orders)
            {
                if (item.IsOpen)
                {
                    item.State = OrderState.Canceled;
                    ++affectedOrders;
                }
            }
            await Task.Run(() => { Thread.Sleep(3000); });
            return affectedOrders;
        }

        public static InstrumentInfo GetInfo(Instrument instrument)
        {
            var info = new InstrumentInfo()
            {
                Instrument = instrument,
                BestBid = Math.Round(decimal.Parse((Random.Shared.Next(20000) + Random.Shared.NextDouble()).ToString()), 2),
            };
            info.BestOffer = Math.Round(decimal.Parse((Random.Shared.Next(int.Parse(Math.Round(info.BestBid, 0).ToString()), 21000) + Random.Shared.NextDouble()).ToString()), 2);



            info.LastPrice = Math.Round((info.BestOffer - info.Spread + decimal.Parse(Random.Shared.NextDouble().ToString())));
            return info;
        }
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
