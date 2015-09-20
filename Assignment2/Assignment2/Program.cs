using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;

namespace Assignment2
{
    /*
     * Delegates for the events
     */
    public delegate void priceCutEvent(int newPrice, int oldPrice);
    public delegate void newOrderEvent(string strOrder);
    public delegate void orderCompletedEvent(int senderID);

    /*
     * Class Name : ChickenFarm 
     * ChickenFarm is a class on the server side.
     * It will be started as a thread by the Main method and will perform a number
     * of service functions. 
     */
    public class ChickenFarm
    {
        private int chickenPrice = 1000;
        static Random rng = new Random();
        public event priceCutEvent priceCut;

        /*
         * Constructor
         */
        public ChickenFarm()
        {
        }

        /*
         * changePrice
         * If the new price is lower than the previous price, then a event needs to generated and all subscribed threads
         * needs to be notified.
         */
        public void changePrice(int price)
        {
            Console.WriteLine("Chicken Farm ***** Price Change : Old Price = {0}   New Price = {1} *****", this.chickenPrice, price);
            if (price < this.chickenPrice)
            {
                int prev = this.chickenPrice;
                this.chickenPrice = price;
                onPriceCutEvent(this.chickenPrice, prev);
            }
            else
            {
                this.chickenPrice = price;
                Console.WriteLine("No Price Cut");
            }
        }

        /*
         * onPriceCutEvent
         * Function that is called on a price cut event occurs
         */
        public void onPriceCutEvent(int newPrice, int oldPrice)
        {
            if (priceCut != null)
            {
                priceCut(newPrice, oldPrice);
            }
        }

        /*
         * farmerFunc
         * This function is a thread function and it is used to generate a new prices for Chicken Farm
         */
        public void farmerFunc()
        {
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(1000);
                int price = pricingModel();
                changePrice(price);
            }
        }

        /* 
         * pricingModel
         * This model generates a random price, so the prices can be expected to go up as well as down.
         */
        public int pricingModel()
        {
            return rng.Next(500, 1000);
        }
    }

    /*
     * Class Name : Retailer
     * Retailer1 through RetailerN, where N = 5, each retailer is a thread 
     * instantiated from the same class (or the same method) in a class. 
     * The retailers’ actions are event-driven.
     * */
    public class Retailer
    {
        static Random rng = new Random();
        private int retailerID;
        private MultiCellBuffer buffer;
        private long cardNumber;

        /*
         * Parameterized Constructor
         */
        public Retailer(int id, MultiCellBuffer buffer, CreditCardHelper cch)
        {
            this.retailerID = id;
            this.buffer = buffer;
            this.cardNumber = cch.getCardNo(retailerID);
        }

        /*
         * chickensOnSale
         * This is an Event Handler function.
         */
        public void chickensOnSale(int price, int prev)
        {
            OrderClass orderObj = new OrderClass(this.retailerID, this.cardNumber, getNoOfChickens(), price);
            object ord = (object)orderObj;
            Thread chickenOnSale_thread = new Thread(new ParameterizedThreadStart(placeOrderFunc));
            chickenOnSale_thread.Start(ord);
        }

        /*
         * placeOrderFunc
         * It is a thread function to place an order of chickens.
         */
        public void placeOrderFunc(object order)
        {
            Thread.Sleep(200);
            OrderClass orderObj = (OrderClass)order;
            Encoder encoder = new Encoder();
            string strOrderEncoded = encoder.encodeObjectToString(orderObj);
            this.buffer.setOneCell(strOrderEncoded);
        }

        /*
         * getNoOfChickens
         * It is Chicken Model to calculate the number of chickens needed (random number)
         * by a Retailer.
         */
        public int getNoOfChickens()
        {
            return rng.Next(1, 10);
        }

        /*
         * confirmationFunc
         * Event Function. Called when the ordered has been completely processed by the thrad to notify the
         * retailer that its order has been completed.
         */
        public void confirmationFunc(int senderID)
        {
            Console.WriteLine("Chicken Farm to Retailer {0} : Your Order has been completed successfully", senderID);
        }
    }

    /*
     * Class Name : OrderProcessing
     * Whenever an order needs to be processed, a new thread is instantiated 
     * from this class (or method) to process the order. It will check the 
     * validity of the credit card number and if correct, then it completes the
     * transaction and generates an event to Retailer to indicate the same.
     */
    public class OrderProcessing
    {
        private Random rng = new Random();
        private CreditCardHelper creditCardHelper;
        private MultiCellBuffer buffer;
        private object lockOrderObj = new object();
        public event orderCompletedEvent orderCompleted;

        /*
         * Constructor
         */
        public OrderProcessing(MultiCellBuffer buffer, CreditCardHelper cch)
        {
            this.creditCardHelper = cch;
            this.buffer = buffer;
        }

        /*
         * orderProcessing
         * Methods to calculate the prices and process the order
         */
        public void orderProcessing(string strOrder)
        {
            try
            {
                Decoder decoder = new Decoder();
                string strProcessOrder = buffer.getOneCell(strOrder);
                OrderClass orderClass = (OrderClass)decoder.decodeStringToObject(strOrder);
                string timeFormat = "HH:mm:ss";
                Console.WriteLine("{0} : Retailer {1} placed an order of {2} Chickens @ ${3}", orderClass.getDateTimeStamp().ToString(timeFormat), orderClass.getSenderID(), orderClass.getNoOfChickens(), orderClass.getAmount());
                Thread orderProcessing_thread = new Thread(new ParameterizedThreadStart(orderProcessingFunc));
                orderProcessing_thread.Start(strOrder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("OrderProcessing : orderProcessing : Message = {0}", ex.Message);
            }
        }

        /*
         * orderProcessingFunc
         * This is a thread function and it is used to generate the Invoice for the retailer
         * and  makes an event signal for the Retailer to confirm that the order has been processes
         * successfully.
         */
        public void orderProcessingFunc(object order)
        {
            try
            {
                int sleepTime = rng.Next(500, 1000);
                Thread.Sleep(sleepTime);
                lock (lockOrderObj)
                {
                    string strOrder = (string)order;
                    Decoder decoder = new Decoder();
                    OrderClass orderObj = (OrderClass)decoder.decodeStringToObject(strOrder);
                    double total = orderObj.getNoOfChickens() * orderObj.getAmount();
                    double tax = total * 10.0 / 100.0;//10 % tax rate on total
                    double shippingCharges = orderObj.getNoOfChickens() * 1.0; //1$ charge for each chicken to be shipped.
                    double totalPrice = total + tax + shippingCharges;

                    if (creditCardHelper.validateCardNumber(orderObj.getSenderID(), orderObj.getCardNo()))
                    {
                        Console.WriteLine("********************************************************************************");
                        Console.WriteLine("Chicken Farm: Card Authenticated");
                        Console.WriteLine("Order processing completed for Retailer {0}", orderObj.getSenderID());
                        Console.WriteLine("No. of Chickens: {0}, Total price: {1}, Time to complete: {2} milliseconds", orderObj.getNoOfChickens(), totalPrice, (DateTime.Now - orderObj.getDateTimeStamp()).Milliseconds);
                        Console.WriteLine("********************************************************************************");
                    }
                    else
                    {
                        Console.WriteLine("Card Authentication Failed for Retailer {0}", orderObj.getSenderID());
                    }
                    onOrderCompleted(orderObj.getSenderID());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Order Processing Failed. Please try again: Message = {0}", ex.Message);
            }
        }

        /*
         * onOrderCompleted
         * It is the event transmitting function to the Retailer.
         */
        public void onOrderCompleted(int senderID)
        {
            if (orderCompleted != null)
                orderCompleted(senderID);
        }
    }

    /*
     * Class Name : OrderClass
     * OrderClass is a class that contains at least the following private data members:
     *      senderId: the identity of the sender, you can use thread name or thread id;
     *      cardNo: an integer that represents a credit card number;
     *      amount: an integer that represents the number of chickens to order;
     */
    public class OrderClass
    {
        private int senderID;
        private long cardNo;
        private int noOfChickens;
        private int amount;
        private DateTime dateTimeStamp;

        /*
         * Constructor
         */
        public OrderClass()
        {
        }

        /*
         * Parameterized Constructor
         */
        public OrderClass(int senderID, long cardNo, int noOfChickens, int amount)
        {
            this.senderID = senderID;
            this.cardNo = cardNo;
            this.noOfChickens = noOfChickens;
            this.amount = amount;
            this.dateTimeStamp = DateTime.Now;
        }

        public int getSenderID()
        {
            return (this.senderID);
        }
        public void setSenderID(int senderID)
        {
            this.senderID = senderID;
        }

        public void setCardNo(long cardNo)
        {
            this.cardNo = cardNo;
        }
        public long getCardNo()
        {
            return (this.cardNo);
        }

        public int getNoOfChickens()
        {
            return (this.noOfChickens);
        }
        public void setNoOfChickens(int noOfChickens)
        {
            this.noOfChickens = noOfChickens;
        }

        public int getAmount()
        {
            return (this.amount);
        }
        public void setAmount(int amount)
        {
            this.amount = amount;
        }

        public DateTime getDateTimeStamp()
        {
            return (this.dateTimeStamp);
        }
        public void setDateTimeStamp(DateTime dateTimeStamp)
        {
            this.dateTimeStamp = dateTimeStamp;
        }
    }

    /*
     * Class Name : MultiCellBuffer
     * MultiCellBuffer class is used for sending the order from the retailers (clients) to the 
     * chickenFarm (server). This class has n data cells. The number of cells is less than the 
     * max number N of retailers in the experiment.
     * 
     */
    public class MultiCellBuffer
    {
        public Semaphore bufferSemaphore;
        private string[] bufferArray;
        private int bufferCapacity;
        public event newOrderEvent newOrder;

        /*
         * Constructor
         */
        public MultiCellBuffer(int noOfCells)
        {
            bufferCapacity = noOfCells;
            bufferSemaphore = new Semaphore(0, noOfCells);
            bufferArray = new string[noOfCells];
            for (int i = 0; i < bufferCapacity; i++)
            {
                bufferArray[i] = "";
            }
        }

        /*
         * Set one cell method
         */
        public void setOneCell(string strOrder)
        {
            try
            {
                bufferSemaphore.WaitOne();
                Boolean setFlag = false;
                for (int i = 0; i < bufferCapacity; i++)
                {
                    lock (bufferArray[i])
                    {
                        if ((bufferArray[i].Equals("")) && (setFlag == false))
                        {
                            setFlag = true;
                            bufferArray[i] = strOrder;
                        }
                    }
                }
                if (setFlag == true)
                {
                    onNewOrder(strOrder);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("MultiCellBuffer :  setOneCell : Exception Message = {0}", e.Message);
            }
        }

        /*
         * onNewOrder
         * This function is used to send an event whenever a new order is added into
         * the buffer and it can be processed.
         */
        public void onNewOrder(string order)
        {
            if (newOrder != null)
            {
                newOrder(order);
            }
        }

        /*
         * getOneCell
         * Get one cell method. It is used to get one cell to user and also it removes that particular cell
         * from the buffer.
         */
        public string getOneCell(string strOrder)
        {
            string retValue = "";
            try
            {
                Boolean getFlag = false;
                for (int i = 0; i < bufferCapacity; i++)
                {
                    lock (bufferArray[i])
                    {
                        if ((bufferArray[i] == strOrder) && (getFlag == false))
                        {
                            retValue = bufferArray[i];
                            bufferArray[i] = "";
                            getFlag = true;
                            bufferSemaphore.Release();
                        }
                    }
                }
                return retValue;
            }
            catch (Exception e)
            {
                Console.WriteLine("MultiCellBuffer :  getOneCell : Exceptiom Message = {0}", e.Message);
            }
            return retValue;
        }
    }

    /*
     * Class Name : Encoder
     * The Encoder class will convert an OrderObject into a string. 
     */
    public class Encoder
    {
        //Password for Encoding
        const string passphrase = "Ankit@123";

        /*
         * encryptData
         * This function used to decrypt the string to human readble string.
         * 
         * Source:http://www.codeproject.com/Tips/306620/Encryption-Decryption-Function-in-Net-using-MD-Cry
         */
        private static String encryptData(string Message)
        {
            byte[] Results;
            System.Text.UTF8Encoding UTF8 = new System.Text.UTF8Encoding();
            MD5CryptoServiceProvider HashProvider = new MD5CryptoServiceProvider();
            byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(passphrase));
            TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider();
            TDESAlgorithm.Key = TDESKey;
            TDESAlgorithm.Mode = CipherMode.ECB;
            TDESAlgorithm.Padding = PaddingMode.PKCS7;
            byte[] DataToEncrypt = UTF8.GetBytes(Message);
            try
            {
                ICryptoTransform Encryptor = TDESAlgorithm.CreateEncryptor();
                Results = Encryptor.TransformFinalBlock(DataToEncrypt, 0, DataToEncrypt.Length);
            }
            finally
            {
                TDESAlgorithm.Clear();
                HashProvider.Clear();
            }
            return Convert.ToBase64String(Results);
        }

        /*
         * encodeObjectToString
         * This function reads the members of the object and coverts it into a human readable 
         * string. Then this is passed to ecryption algorithm, which is no longer
         * comprehedible by human.
         */
        public String encodeObjectToString(OrderClass orderClass)
        {
            int amount = orderClass.getAmount();
            long cardNo = orderClass.getCardNo();
            int noOfChickens = orderClass.getNoOfChickens();
            int senderID = orderClass.getSenderID();
            DateTime timeStamp = orderClass.getDateTimeStamp();
            String strToEncode = amount.ToString() + ":::" + cardNo.ToString() + ":::" + noOfChickens.ToString() + ":::" + senderID.ToString() + ":::" + timeStamp.ToString();
            return encryptData(strToEncode);
        }
    }

    /*
     * Class Name : Decoder
     * The Decoder will convert the string back into the OrderObject. 
     */
    public class Decoder
    {
        //Password for Decoding
        const string passphrase = "Ankit@123";

        /*
         * decodeStringToObject
         * This function reads splits the human readable string and populates
         * the OrderClass object accordingly.
         */
        public OrderClass decodeStringToObject(String inputEncrypted)
        {
            string inputDecrpyted = decryptData(inputEncrypted);
            string[] stringSeparators = new string[] { ":::" };
            string[] str = inputDecrpyted.Split(stringSeparators, StringSplitOptions.None);
            int amount = Convert.ToInt32(str[0]);
            long cardNo = Convert.ToInt64(str[1]);
            int noOfChickens = Convert.ToInt32(str[2]);
            int senderID = Convert.ToInt32(str[3]);
            DateTime dateTimeStamp = Convert.ToDateTime(str[4]);

            OrderClass orderObj = new OrderClass();
            orderObj.setAmount(amount);
            orderObj.setCardNo(cardNo);
            orderObj.setNoOfChickens(noOfChickens);
            orderObj.setSenderID(senderID);
            orderObj.setDateTimeStamp(dateTimeStamp);

            return orderObj;
        }

        /*
         * decryptData
         * This function used to decrypt the string to human readble string.
         * 
         * Source:http://www.codeproject.com/Tips/306620/Encryption-Decryption-Function-in-Net-using-MD-Cry
         */
        private static string decryptData(string Message)
        {
            byte[] Results;
            System.Text.UTF8Encoding UTF8 = new System.Text.UTF8Encoding();
            MD5CryptoServiceProvider HashProvider = new MD5CryptoServiceProvider();
            byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(passphrase));
            TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider();
            TDESAlgorithm.Key = TDESKey;
            TDESAlgorithm.Mode = CipherMode.ECB;
            TDESAlgorithm.Padding = PaddingMode.PKCS7;
            byte[] DataToDecrypt = Convert.FromBase64String(Message);
            try
            {
                ICryptoTransform Decryptor = TDESAlgorithm.CreateDecryptor();
                Results = Decryptor.TransformFinalBlock(DataToDecrypt, 0, DataToDecrypt.Length);
            }
            finally
            {
                TDESAlgorithm.Clear();
                HashProvider.Clear();
            }
            return UTF8.GetString(Results);
        }
    }

    /*
     * Class Name : CreditCardHelper
     * This class is used to manage the credit card details for the retailers and hence
     * it can also be used for verification of the retailers card numbers.
     */
    public class CreditCardHelper
    {
        private long[] cardNo;
        private Random rng = new Random();

        /*
         * Constructor 
         */
        public CreditCardHelper(int N)
        {
            cardNo = new long[N];
        }

        /*
         * getCardNo
         * This Method generates a card number for a retailer as well as save the
         * card number into an array for verification at a later stage.
         */
        public long getCardNo(int id)
        {
            string strCardNumber = "";
            string strID = id.ToString();
            for (int i = 0; i < 16; i++)
            {
                strCardNumber += strID;
            }
            cardNo[id] = Convert.ToInt64(strCardNumber);
            return cardNo[id];
        }

        /*
         * validateCardNumber
         * This function validates the card number with retailer specific card number
         */
        public bool validateCardNumber(int id, long cardNumber)
        {
            if (cardNo[id] == cardNumber)
                return true;
            else
                return false;
        }
    }

    /*
     * Class Name : Program
     * This the class that contains the main method from where the program execution begins.
     */
    class Program
    {
        static void Main(string[] args)
        {
            int noOfBufferCells = 3; // Buffer Capacity- Number of cells in Multicellbuffer
            int N = 5; //Number of Reatilers

            ChickenFarm chickenFarm = new ChickenFarm();
            Thread chickenFarm_thread = new Thread(new ThreadStart(chickenFarm.farmerFunc));

            CreditCardHelper creditCardHelper = new CreditCardHelper(N);
            MultiCellBuffer buffer = new MultiCellBuffer(noOfBufferCells);

            buffer.bufferSemaphore.Release(noOfBufferCells);
            OrderProcessing orderProcessing = new OrderProcessing(buffer, creditCardHelper);

            chickenFarm_thread.Name = "ChickenFarm";
            chickenFarm_thread.Start();
            Retailer retailer = null;
            for (int i = 0; i < N; i++)
            {
                retailer = new Retailer(i, buffer, creditCardHelper);
                chickenFarm.priceCut += new priceCutEvent(retailer.chickensOnSale);
            }
            buffer.newOrder += new newOrderEvent(orderProcessing.orderProcessing);
            orderProcessing.orderCompleted += new orderCompletedEvent(retailer.confirmationFunc);
            Console.ReadKey();
        }
    }
}
