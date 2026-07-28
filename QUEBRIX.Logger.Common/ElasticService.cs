using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QUEBRIX.Logger.Common;

//public interface IElasticService
//{
//    Task<bool> AddDoc<T>(T Doc);
//    Task<bool> SetDoc<T>(T Doc);
//    Task<bool> DeleteDoc<T>(T Doc);
//}


//public class ElasticService : IElasticService
//{
//    public async Task<bool> AddDoc<T>(T Doc) where T : BaseDoc
//    {
//        if (Objects.Client == null)
//            return false;
//        if (Doc == null || string.IsNullOrEmpty(Doc.Id.ToString()))
//            return false;
//        var response = Objects.Client.Indices.Exists(typeof(T).Name.ToLower() + "_" + Doc.ManagementAccountId);

//        if (!response.Exists)
//        {
//            var createIndexResponse = await Objects.Client.Indices.CreateAsync(typeof(T).Name.ToLower() + "_" + Doc.ManagementAccountId, c => c.Map<T>(m => m.AutoMap()));
//        }
//        var updateResponse = await Objects.Client.IndexAsync(Doc, i => i.Id(Doc.Id.ToString()).Index(typeof(T).Name.ToLower() + "_" + Doc.ManagementAccountId)
//                        );
//        if (updateResponse.IsValid)
//            return true;
//        else
//            return false;
//    }



//    public async Task<bool> SetDoc<T>(T Doc) where T : BaseDoc
//    {
//        if (Objects.Client == null)
//            return false;
//        if (Doc == null || string.IsNullOrEmpty(Doc.Id.ToString()))
//            return false;
//        var response = Objects.Client.Indices.Exists(typeof(T).Name.ToLower() + "_" + Doc.ManagementAccountId);
//        if (!response.Exists)
//        {
//            var createIndexResponse = await Objects.Client.Indices.CreateAsync(typeof(T).Name.ToLower() + "_" + Doc.ManagementAccountId, c => c.Map<T>(m => m.AutoMap()));
//        }
//        var updateResponse = await Objects.Client.UpdateAsync<T>(Doc.Id.ToString(), u => u.Index(typeof(T).Name.ToLower() + "_" + Doc.ManagementAccountId).Doc(Doc).DocAsUpsert());
//        if (updateResponse.IsValid)
//            return true;
//        else
//            return false;
//    }

//    public async Task<bool> DeleteDoc<T>(T Doc) where T : BaseDoc
//    {
//        if (Objects.Client == null)
//            return false;
//        if (Doc == null || string.IsNullOrEmpty(Doc.Id.ToString()))
//            return false;
//        var updateResponse = await Objects.Client.DeleteAsync<T>(Doc.Id.ToString(), u => u.Index(typeof(T).Name.ToLower() + "_" + Doc.ManagementAccountId));
//        if (updateResponse.IsValid)
//            return true;
//        else
//            return false;
//    }

//    public async Task<bool> BulkAdd<T>(List<T> Doc) where T : BaseDoc
//    {
//        var bulkDescriptor = new BulkDescriptor();

//        foreach (var document in Doc)
//        {
//            bulkDescriptor.Index<T>(op => op
//                .Document(document)
//                .Index(typeof(T).Name.ToLower() + "_" + Doc.FirstOrDefault()?.ManagementAccountId)
//                .Id(document.Id)
//            );
//        }
//        var bulkResponse = await Objects.Client.BulkAsync(bulkDescriptor);
//        if (bulkResponse.Errors)
//            return false;
//        else
//            return true;
//    }
//}