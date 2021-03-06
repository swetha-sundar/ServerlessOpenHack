
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using Microsoft.Azure.Documents.Client;
using System.Text;
using System.Collections.Generic;

namespace Oteam15.Function
{
    public static class CreateRatings
    {
        private static readonly HttpClient httpClient;
        static CreateRatings()
        {
            httpClient = new HttpClient();
        }

        [FunctionName("CreateRatings")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]Rating data,
        [CosmosDB(
                databaseName: "Ratings",
                collectionName: "ratings",
                ConnectionStringSetting = "CosmosDBConnection")] IAsyncCollector<Rating> documentsToStore, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            // string requestBody = new StreamReader(req.Body).ReadToEnd();
            // RatingData data = JsonConvert.DeserializeObject<RatingData>(requestBody);
            string errorField = "";
            if(data != null){
                bool productValid = await ProductIdValid(data.productId.ToString());
                bool userValid = await UserIdValid(data.userId.ToString());
                bool ratingValid = data.rating >= 0 && data.rating <=5;
                if( productValid && userValid && ratingValid ){
                    data.id = Guid.NewGuid();
                    data.timestamp = DateTime.UtcNow;

                    try{
                        var sd = new SentimentReq(){
                        documents = new List<SentimentReqItem>(){new SentimentReqItem(){id=data.id.ToString(),text=data.userNotes}}};
                        var sentimentResp = await GetRatingScore(sd);
                        data.sentimentScore = sentimentResp.documents[0].score;

                   
                        log.LogMetric("RatingSentiments",sentimentResp.documents[0].score, new Dictionary<string,object>(){
                            {"UserNote",data.userNotes},
                            {"RatingId",data.id}
                            });
                        await documentsToStore.AddAsync(data);
                        //
                    }
                    catch(Exception e){
                        return new BadRequestObjectResult("CosmosDB error: " + e.Message);
                    }
                    return (ActionResult)new OkObjectResult(data);
                }
                else if (!productValid)
                    errorField = "productId";
                else if (!userValid)
                    errorField = "userId";
                else if (!ratingValid)
                    errorField = "rating value";
                          
            }
            return new BadRequestObjectResult("meow3 please input valid " + errorField);

        }



        private static async Task<SentimentResp> GetRatingScore(SentimentReq sd){
            //https://southeastasia.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment
            var taUrl = "https://southeastasia.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment";
            var postData = JsonConvert.SerializeObject(sd);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, taUrl);
            request.Content = new StringContent(postData,
                                                Encoding.UTF8, 
                                                "application/json");
            request.Content.Headers.Add("Ocp-Apim-Subscription-Key","987bb1fbe0e54280b15341cfa8d45f26");

            var result = await httpClient.SendAsync(request);
            var ret = await result.Content.ReadAsAsync<SentimentResp>();
            
            return ret;
        }
        private static async Task<bool> ProductIdValid(string productId){
            //https://serverlessohproduct.trafficmanager.net/api/GetProduct?productid=75542e38-563f-436f-adeb-f426f1dabb5c
            //{"productId":"75542e38-563f-436f-adeb-f426f1dabb5c","productName":"Starfruit Explosion","productDescription":"This starfruit ice cream is out of this world!"}
            var host = "https://oteam15apim.azure-api.net/fn/GetProduct?productid=";
            try {
                if(!string.IsNullOrWhiteSpace(productId)){
                    var result = await httpClient.GetStringAsync(string.Format(host+productId));
                    dynamic dynresult = JsonConvert.DeserializeObject(result);
                    var ret = (productId.Equals((string)(dynresult.productId)));
                    return ret;
                }
            }
            catch (Exception){
                return false;
            }
         return false;
        }
        private static async Task<bool> UserIdValid(string userId){
            //https://serverlessohuser.trafficmanager.net/api/GetUser?userId=cc20a6fb-a91f-4192-874d-132493685376
            //{"userId":"cc20a6fb-a91f-4192-874d-132493685376","userName":"doreen.riddle","fullName":"Doreen Riddle"}
            var host = "https://oteam15apim.azure-api.net/fn/GetUser?userId=";
            try {
                if(!string.IsNullOrWhiteSpace(userId)){
                    var result = await httpClient.GetStringAsync(string.Format(host+userId));
                    dynamic dynresult = JsonConvert.DeserializeObject(result);
                    var ret = (userId.Equals((string)(dynresult?.userId)));
                    return ret;
                }
            }
            catch (Exception){
                return false;
            }
         return false;
        }
    }


    /*
{
    "userId": "cc20a6fb-a91f-4192-874d-132493685376",
    "productId": "4c25613a-a3c2-4ef3-8e02-9c335eb23204",
    "locationName": "Sample ice cream shop",
    "rating": 5,
    "userNotes": "I love the subtle notes of orange in this ice cream!"
}
     */
}
