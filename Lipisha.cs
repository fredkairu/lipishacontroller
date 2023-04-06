using System;
using System.Net;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using System.Web.Mvc.Ajax;
using Swype.OsiModel;

namespace Swype.Controllers
{

    public class LipishaData
    {
        public string api_key { get; set; }
        public string api_signature { get; set; }
        public string api_type { get; set; }
        public string transaction_reference { get; set; }
        public string transaction_mobile { get; set; }
        public float transaction_amount { get; set; }
        public string transaction_status { get; set; }
        public string transaction_status_code { get; set; }
        public string transaction_status_description { get; set; }
        public string transaction_response { get;  set; }

        public bool authenticate(string apiKey, string apiSignature)
        {
            return api_key == apiKey && apiSignature == api_signature;
        }
    }
    [Authorize]
    public class LipishaController : Controller
    {
        private const string API_KEY = "79b60b46966ba72205022de53bdbbc1a";
        private const string API_SIGNATURE = "0N83KkK4jk3l9hNjtTFvR9Q4ELqkUr/RmTiEsPuD5ggVUM9hxRkVyh+wUEtl1V086vozMArg0pGpx9gP4fcJZrBib+CGudxwA1BIXk83YdQgPggn6RQoLSByyVEgUEw2t3ROMEw87ECulI6NzdTzZE2rFRZLLPCfuq9d7bCFj88=";
        private const string API_VERSION = "1.3.0";
        private const string ACTION_INITIATE = "Initiate";
        private const string ACTION_ACKNOWLEDGE = "Acknowledge";
        private const string ACTION_RECEIPT = "Receipt";
        private const string STATUS_SUCCESS = "Success";
        private const string STATUS_SUCCESS_CODE = "001";
        private OsiDbEntities db = new OsiDbEntities();

        [HttpPost]
        public ActionResult Index(LipishaData lipishaData)
        {
            Dictionary<string, string> response = new Dictionary<string, string>();
            if (lipishaData.authenticate(API_KEY, API_SIGNATURE))
            {
                if (lipishaData.api_type == ACTION_INITIATE)
                {
                    // Respond to Lipisha confirming receipt of payment IPN push
                    // We can store the transaction in draft state awaiting acknowledgement
                    response.Add("api_key", API_KEY);
                    response.Add("api_signature", API_SIGNATURE);
                    response.Add("api_type", ACTION_RECEIPT);
                    response.Add("transaction_reference", lipishaData.transaction_reference);
                    response.Add("transaction_status_code", STATUS_SUCCESS_CODE);
                    response.Add("transaction_status", STATUS_SUCCESS);
                    response.Add("transaction_status_description", "Transaction Received");
                    response.Add("transaction_custom_sms", "Payment Received. Thank you.");
                }
                else if (lipishaData.api_type == ACTION_ACKNOWLEDGE || lipishaData.api_type == ACTION_RECEIPT)
                {
                    // Lipisha will then send an acknowledgement of this confirmation
                    // At this point we can update the transaction received in the initiate action
                    // above.
                    Console.WriteLine("Transaction: Reference: " + lipishaData.transaction_reference);
                    Console.WriteLine("Transaction: Status: " + lipishaData.transaction_status);
                    response.Add("status", "OK");
                    //find fan from fomatted phone
                    String Telephone = formatTelephone(lipishaData.transaction_mobile);
                    var fan = db.Fans.Where(f => f.Telephone == Telephone).FirstOrDefault();
                    if (fan == null)
                    {
                        lipishaData.transaction_response = "NO_FAN_ACCOUNT";
                        return Json(lipishaData);
                    }
                    else {
                        FanFinanceAccount financeAccount = db.FanFinanceAccounts.Where(f => f.IdFan == fan.idFan).FirstOrDefault();
                        var idReference = Guid.NewGuid();
                        createCreditEntry(financeAccount, lipishaData.transaction_amount, idReference);
                        CreditAccount(financeAccount, lipishaData.transaction_amount);
                       
                        return Json(financeAccount);
                    }
                        //find fan finace account from id fan
                        //create credit for fan

                    }
                else {
                    //  return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Unknown Request");
                    return Json(lipishaData);
                }
            }
            else {
                return Json(lipishaData);
                //return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "Bad Unauthorized");
            }

            return Json(response);
        }

        private void createCreditEntry(FanFinanceAccount fanFinanceAccount, float amount, Guid idReference)
        {
            FanAccountCredit credit = new FanAccountCredit();
            credit.idCredit = idReference;
            credit.amount = double.Parse(amount.ToString());
            credit.creditTime = DateTime.UtcNow.AddHours(3).ToString();
            credit.accountCredited = fanFinanceAccount.idFinanceAccount;
            credit.creditedBy = "Mobile";
            db.FanAccountCredits.Add(credit);
        }
        private void CreditAccount(FanFinanceAccount fanFinanceAccount, float amount)
        {
            fanFinanceAccount.balance += amount;
        }
        private string formatTelephone(string _telephone)
        {
            String str = '0' + _telephone.Remove(0, 3);
            return str ;
        }
    }
}

