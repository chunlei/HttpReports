﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HttpReports.Core.Config;
using HttpReports.Core.Models;
using HttpReports.Dashboard.DTO;
using HttpReports.Dashboard.Implements;
using HttpReports.Dashboard.Models;
using HttpReports.Dashboard.Services;
using HttpReports.Dashboard.Services.Quartz;
using HttpReports.Dashboard.ViewModels;
using HttpReports.Models;
using HttpReports.Monitor;
using HttpReports.Storage.FilterOptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HttpReports.Dashboard.Controllers
{
    public class HttpReportsDataController : Controller
    {
        private readonly IHttpReportsStorage _storage;

        private readonly MonitorService _monitorService;

        private readonly ScheduleService _scheduleService; 

        public HttpReportsDataController(IHttpReportsStorage storage, MonitorService monitorService, ScheduleService scheduleService)
        {
            _storage = storage;
            _monitorService = monitorService;
            _scheduleService = scheduleService;
        }

        public async Task<IActionResult> GetIndexChartData(GetIndexDataRequest request)
        {
            var start = (request.Start.IsEmpty() ? DateTime.Now.ToString("yyyy-MM-dd") : request.Start).ToDateTime();
            var end = (request.End.IsEmpty() ? DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") : request.End).ToDateTime();

            var nodes = request.Node.IsEmpty() ? null : request.Node.Split(',');

            var topRequest = await _storage.GetUrlRequestStatisticsAsync(new RequestInfoFilterOption()
            {
                Nodes = nodes,
                StartTime = start,
                EndTime = end,
                IsAscend = false,
                Take = request.TOP,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss" 
            }).ConfigureAwait(false);

            var topError500 = await _storage.GetUrlRequestStatisticsAsync(new RequestInfoFilterOption()
            {
                Nodes = nodes,
                StartTime = start,
                EndTime = end,
                IsAscend = false,
                Take = request.TOP,
                StatusCodes = new[] { 500 },
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss"
            }).ConfigureAwait(false);

            var fast = await _storage.GetRequestAvgResponeTimeStatisticsAsync(new RequestInfoFilterOption()
            {
                Nodes = nodes,
                StartTime = start,
                EndTime = end,
                IsAscend = true,
                Take = request.TOP,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss"
            }).ConfigureAwait(false);

            var slow = await _storage.GetRequestAvgResponeTimeStatisticsAsync(new RequestInfoFilterOption()
            {
                Nodes = nodes,
                StartTime = start,
                EndTime = end,
                IsAscend = false,
                Take = request.TOP,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss"

            }).ConfigureAwait(false);

            var Art = new
            {
                fast = fast.Select(m => new EchartPineDataModel(m.Url, (int)m.Time)),
                slow = slow.Select(m => new EchartPineDataModel(m.Url, (int)m.Time))
            };

            var StatusCode = (await _storage.GetStatusCodeStatisticsAsync(new RequestInfoFilterOption()
            {
                Nodes = nodes,
                StartTime = start,
                EndTime = end,
                StatusCodes = new[] { 200, 301, 302, 303, 400, 401, 403, 404, 500, 502, 503 },
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss"
            }).ConfigureAwait(false)).Where(m => true).Select(m => new EchartPineDataModel(m.Code.ToString(), m.Total)).ToArray();

            var ResponseTime = (await _storage.GetGroupedResponeTimeStatisticsAsync(new GroupResponeTimeFilterOption()
            {
                Nodes = nodes,
                StartTime = start,
                EndTime = end,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss"
            }).ConfigureAwait(false)).Where(m => true).Select(m => new EchartPineDataModel(m.Name, m.Total)).ToArray();

            return Json(new HttpResultEntity(1, "ok", new { StatusCode, ResponseTime, topRequest, topError500, Art }));
        }

        public async Task<IActionResult> GetDayStateBar(GetIndexDataRequest request)
        {
            var startTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0, DateTimeKind.Local).AddDays(-1);
            var endTime = DateTime.Now; 

            var nodes = request.Node.IsEmpty() ? null : request.Node.Split(',');

            // 每小时请求次数
            var requestTimesStatistics = await _storage.GetRequestTimesStatisticsAsync(new TimeSpanStatisticsFilterOption()
            {
                StartTime = startTime,
                EndTime = endTime,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss",
                Nodes = nodes,
                Type = TimeUnit.Hour,
            }).ConfigureAwait(false);

            //每小时平均处理时间
            var responseTimeStatistics = await _storage.GetResponseTimeStatisticsAsync(new TimeSpanStatisticsFilterOption()
            {
                StartTime = startTime,
                EndTime = endTime,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss",
                Nodes = nodes,
                Type = TimeUnit.Hour,
            }).ConfigureAwait(false);

            List<int> timesList = new List<int>();
            List<int> avgList = new List<int>();

            List<int> hours = new List<int>(); 

            for (int i = 1; i <= 24 ; i++)
            {
                var start = startTime.AddHours(i).ToString("dd-HH"); 

                // 每小时请求次数
                var times = requestTimesStatistics.Items.TryGetValue(start,out var tTimes) ? tTimes : 0;
                timesList.Add(times);


                //每小时平均处理时间
                var avg = responseTimeStatistics.Items.TryGetValue(start, out var tAvg) ? tAvg : 0;
                avgList.Add(avg);


                hours.Add(startTime.AddHours(i).ToString("HH").ToInt());

            }  

            return Json(new HttpResultEntity(1, "ok", new { timesList, avgList, hours }));
        }

        public async Task<IActionResult> GetMinuteStateBar(GetIndexDataRequest request)
        {
            var startTime = DateTime.Now.AddHours(-1).AddSeconds(-DateTime.Now.Second);
           
            var endTime = DateTime.Now;

            var nodes = request.Node.IsEmpty() ? null : request.Node.Split(',');

            // 每小时请求次数
            var requestTimesStatistics = await _storage.GetRequestTimesStatisticsAsync(new TimeSpanStatisticsFilterOption()
            {
                StartTime = startTime,
                EndTime = endTime,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss",
                Nodes = nodes,
                Type = TimeUnit.Minute,
            }).ConfigureAwait(false);

            //每小时平均处理时间
            var responseTimeStatistics = await _storage.GetResponseTimeStatisticsAsync(new TimeSpanStatisticsFilterOption()
            {
                StartTime = startTime,
                EndTime = endTime,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss",
                Nodes = nodes,
                Type = TimeUnit.Minute,
            }).ConfigureAwait(false);

            List<int> timesList = new List<int>();
            List<int> avgList = new List<int>();

            List<int> time = new List<int>();

            for (int i = 1; i <= 60; i++)
            {
                var start = startTime.AddMinutes(i).ToString("HH-mm");

                // 每小时请求次数
                var times = requestTimesStatistics.Items.TryGetValue(start, out var tTimes) ? tTimes : 0;
                timesList.Add(times);


                //每小时平均处理时间
                var avg = responseTimeStatistics.Items.TryGetValue(start, out var tAvg) ? tAvg : 0;
                avgList.Add(avg);


                time.Add(startTime.AddMinutes(i).ToString("mm").ToInt());

            }

            return Json(new HttpResultEntity(1, "ok", new { timesList, avgList, time }));
        }

        public async Task<IActionResult> GetLatelyDayChart(GetIndexDataRequest request)
        {
            var startTime = DateTime.Now.Date.AddDays(-31);
            var endTime = DateTime.Now.Date.AddDays(1).AddSeconds(-1);
            var nodes = request.Node.IsEmpty() ? null : request.Node.Split(',');

            var responseTimeStatistics = await _storage.GetRequestTimesStatisticsAsync(new TimeSpanStatisticsFilterOption()
            {
                StartTime = startTime,
                EndTime = endTime,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss",
                Nodes = nodes,
                Type = TimeUnit.Day,
            }).ConfigureAwait(false);

            List<string> time = new List<string>();
            List<int> value = new List<int>();


            var monthDayCount = (endTime - startTime).Days;
            for (int i = 1; i <= monthDayCount; i++)
            {
                var day = startTime.AddDays(i).ToString("yyyy-MM-dd");

                var times = responseTimeStatistics.Items.TryGetValue(day, out var tTimes) ? tTimes : 0;

                time.Add(startTime.AddDays(i).ToString("dd").ToInt().ToString());
                value.Add(times); 
            }  

            return Json(new HttpResultEntity(1, "ok", new { time, value }));
        }

        public async Task<IActionResult> GetMonthDataByYear(GetIndexDataRequest request)
        {
            var startTime = $"{request.Year}-01-01".ToDateTimeOrDefault(() => new DateTime(DateTime.Now.Year, 1, 1));
            var endTime = startTime.AddYears(1);
            var nodes = request.Node?.Split(',');

            var responseTimeStatistics = await _storage.GetRequestTimesStatisticsAsync(new TimeSpanStatisticsFilterOption()
            {
                StartTime = startTime,
                EndTime = endTime,
                Nodes = nodes,
                Type = TimeUnit.Month,
            }).ConfigureAwait(false);

            List<string> time = new List<string>();
            List<int> value = new List<int>();

            string Range = $"{request.Year}-01-{request.Year}-12";

            for (int i = 0; i < 12; i++)
            {
                var month = (i + 1).ToString();

                var times = responseTimeStatistics.Items.TryGetValue(month, out var tTimes) ? tTimes : 0;

                time.Add(month);
                value.Add(times);
            }

            return Json(new HttpResultEntity(1, "ok", new { time, value, Range }));
        }

        public IActionResult GetTimeRange(int Tag)
        {
            //TODO 将此函数放到前端直接获取
            if (Tag == 1)
            {
                return Json(new HttpResultEntity(1, "ok", new
                {
                    start = DateTime.Now.ToString("yyyy-MM-dd"),
                    end = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd")
                }));
            }

            if (Tag == 2)
            {
                return Json(new HttpResultEntity(1, "ok", new
                {
                    start = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"),
                    end = DateTime.Now.ToString("yyyy-MM-dd")
                }));
            }

            if (Tag == 3)
            {
                return Json(new HttpResultEntity(1, "ok", new
                {
                    start = DateTime.Now.AddDays(-((int)DateTime.Now.DayOfWeek == 0 ? 7 : (int)DateTime.Now.DayOfWeek) + 1).ToString("yyyy-MM-dd"),
                    end = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd")
                }));
            }

            if (Tag == 4)
            {
                return Json(new HttpResultEntity(1, "ok", new
                {
                    start = DateTime.Now.AddDays(-DateTime.Now.Day + 1).ToString("yyyy-MM-dd"),
                    end = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd")
                }));
            }

            return NoContent();
        }

        public IActionResult GetTimeTag(string start, string end, int TagValue)
        {
            //TODO 将此函数放到前端直接获取
            var result = new HttpResultEntity<int>(1, "ok", 0);
            if (TagValue > 0)
            {
                if (TagValue == 1 && start == DateTime.Now.ToString("yyyy-MM-dd") && end == DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"))
                {
                    return Json(new HttpResultEntity<int>(1, "ok", -1));
                }

                if (TagValue == 2 && start == DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd") && end == DateTime.Now.ToString("yyyy-MM-dd"))
                {
                    return Json(new HttpResultEntity<int>(1, "ok", -1));
                }

                if (TagValue == 3 && start == DateTime.Now.AddDays(-((int)DateTime.Now.DayOfWeek == 0 ? 7 : (int)DateTime.Now.DayOfWeek) + 1).ToString("yyyy-MM-dd") && end == DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"))
                {
                    return Json(new HttpResultEntity<int>(1, "ok", -1));
                }

                if (TagValue == 4 && start == DateTime.Now.AddDays(-DateTime.Now.Day + 1).ToString("yyyy-MM-dd") && end == DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"))
                {
                    return Json(new HttpResultEntity<int>(1, "ok", -1));
                }
            }

            if (start.IsEmpty() && end.IsEmpty())
            {
                result = new HttpResultEntity<int>(1, "ok", 1);

                return Json(result);
            }

            if (start == DateTime.Now.ToString("yyyy-MM-dd") && end == DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"))
            {
                result = new HttpResultEntity<int>(1, "ok", 1);

                return Json(result);
            }

            if (start == DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd") && end == DateTime.Now.ToString("yyyy-MM-dd"))
            {
                result = new HttpResultEntity<int>(1, "ok", 2);
                return Json(result);
            }

            if (start == DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek + 1).ToString("yyyy-MM-dd") && end == DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"))
            {
                result = new HttpResultEntity<int>(1, "ok", 3);
                return Json(result);
            }

            if (start == DateTime.Now.AddDays(-DateTime.Now.Day + 1).ToString("yyyy-MM-dd") && end == DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"))
            {
                result = new HttpResultEntity<int>(1, "ok", 4);
                return Json(result);
            }

            return Json(result);
        }

        public async Task<IActionResult> GetIndexData(GetIndexDataRequest request)
        {
            var start = request.Start.ToDateTime();
            var end = request.End.ToDateTime();

            var result = await _storage.GetIndexPageDataAsync(new IndexPageDataFilterOption()
            {
                Nodes = request.Node.IsEmpty() ? null : request.Node.Split(','),
                StartTime = start,
                EndTime = end,
                StartTimeFormat= "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss"
            }).ConfigureAwait(false);

            return Json(new HttpResultEntity(1, "ok", new
            {
                ART = result.AvgResponseTime.ToInt(),
                Total = result.Total,
                Code404 = result.NotFound,
                Code500 = result.ServerError,
                APICount = result.APICount,
                ErrorPercent = result.ErrorPercent.ToString("0.00%"),
            }));
        }

        public async Task<IActionResult> GetRequestList(GetRequestListRequest request)
        {  
            var result = await _storage.SearchRequestInfoAsync(new RequestInfoSearchFilterOption()
            {
                Nodes = request.Node.IsEmpty() ? null : request.Node.Split(','),
                IP = request.IP,
                Url = request.Url,
                StartTime = request.Start.ToDateTime(),
                EndTime = request.End.TryToDateTime(),
                Page = request.pageNumber,
                PageSize = request.pageSize,
                IsOrderByField = true,
                Field = RequestInfoFields.CreateTime,
                IsAscend = false,
                StartTimeFormat = "yyyy-MM-dd HH:mm:ss",
                EndTimeFormat = "yyyy-MM-dd HH:mm:ss"

            }).ConfigureAwait(false);

            return Json(new { total = result.AllItemCount, rows = result.List });
        }

        public async Task<IActionResult> EditMonitor(MonitorJobRequest request)
        {
            string vaild = _monitorService.VaildMonitorJob(request);

            if (!vaild.IsEmpty())
                return Json(new HttpResultEntity(-1, vaild, null));

            IMonitorJob model = _monitorService.GetMonitorJob(request);

            if (request.Id.IsEmpty() || request.Id == "0")
                await _storage.AddMonitorJob(model).ConfigureAwait(false);

            else
                await _storage.UpdateMonitorJob(model).ConfigureAwait(false);

            await _scheduleService.UpdateMonitorJobAsync();

            return Json(new HttpResultEntity(1, "ok", null));
        }

        public async Task<IActionResult> GetMonitor(string Id)
        {
            if (Id.IsEmpty() || Id == "0")
                return NoContent();

            var job = await _storage.GetMonitorJob(Id).ConfigureAwait(false);

            if (job == null)
                return NoContent();

            var request = _monitorService.GetMonitorJobRequest(job);

            return Json(new HttpResultEntity(1, "ok", request));
        }

        public async Task<IActionResult> DeleteJob(string Id)
        {
            await _storage.DeleteMonitorJob(Id).ConfigureAwait(false);

            await _scheduleService.UpdateMonitorJobAsync();

            return Json(new HttpResultEntity(1, "ok", null));
        }

        public async Task<IActionResult> ChangeJobState(string Id)
        {
            var model = await _storage.GetMonitorJob(Id).ConfigureAwait(false);

            model.Status = model.Status == 1 ? 0 : 1;

            await _storage.UpdateMonitorJob(model).ConfigureAwait(false);

            await _scheduleService.UpdateMonitorJobAsync();

            return Json(new HttpResultEntity(1, "ok", null));
        }


        [AllowAnonymous]
        public async Task<IActionResult> CheckUserLogin(SysUser user)
        {
            var model = await _storage.CheckLogin(user.UserName.Trim(), user.Password.Trim().MD5()).ConfigureAwait(false);

            if (model == null)
                return Json(new HttpResultEntity(-1, "用户名或者密码错误", null));

            HttpContext.SetCookie(BasicConfig.LoginCookieId, user.UserName, 60 * 30 * 7);

            return Json(new HttpResultEntity(1, "登录成功", null));
        }

        public async Task<IActionResult> UpdateAccountInfo(UpdateAccountRequest request)
        {
            var user = await _storage.GetSysUser(request.Username).ConfigureAwait(false);

            if (user.Password != request.OldPwd.MD5())
            {
                return Json(new HttpResultEntity(-1, "旧密码错误", null));
            }

            if (request.NewUserName.Length <= 2 || request.NewUserName.Length > 20)
            {
                return Json(new HttpResultEntity(-1, "用户名格式错误", null));
            }

            if (request.OldPwd.Length <= 5 || request.OldPwd.Length > 20)
            {
                return Json(new HttpResultEntity(-1, "新密码格式错误", null));
            }

            await _storage.UpdateLoginUser(new SysUser
            {
                Id = user.Id,
                UserName = request.NewUserName,
                Password = request.NewPwd.MD5()
            });

            return Json(new HttpResultEntity(1, "修改成功", null));
        }

        public async Task<IActionResult> GetTraceList(string Id)
        {
            var parent = await GetGrandParentRequestInfo(Id);

            var tree = await GetRequestInfoTrace(parent.Id);

            return Json(new HttpResultEntity(1,"ok", new List<RequestInfoTrace>() { tree }));
        }


        public async Task<IActionResult> GetRequestInfoDetail(string Id)
        { 
            var (requestInfo, requestDetail) = await _storage.GetRequestInfoDetail(Id); 

            return Json(new HttpResultEntity(1, "ok", new { 
            
                Info = requestInfo,
                Detail = requestDetail  

            }));
        }

        public IActionResult Json(object data)
        {
            HttpContext.Response.ContentType = "application/json;charset=utf-8";

            return Content(JsonConvert.SerializeObject(data,new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
        }

        private async Task<IRequestInfo> GetGrandParentRequestInfo(string Id)
        {
            var requestInfo = await _storage.GetRequestInfo(Id);

            if (requestInfo.ParentId.IsEmpty())
            {
                return requestInfo;
            }
            else
            {
                return await GetGrandParentRequestInfo(requestInfo.ParentId);
            }
        }


        private async Task<RequestInfoTrace> GetRequestInfoTrace(string Id)
        {
            var requestInfo = await _storage.GetRequestInfo(Id);

            var requestInfoTrace = MapRequestInfo(requestInfo);

            var childs = await _storage.GetRequestInfoByParentId(requestInfo.Id);

            if (childs != null && childs.Count > 0 )
            {
                requestInfoTrace.Nodes = new List<RequestInfoTrace>();
            }


            foreach (var item in childs)
            { 
                var child = MapRequestInfo(item);

                var trace = await GetRequestInfoTrace(item.Id);

                requestInfoTrace.Nodes.Add(trace);
            }

            return requestInfoTrace; 
        }

        private RequestInfoTrace MapRequestInfo(IRequestInfo requestInfo)
        {
            return new RequestInfoTrace { 

                 Id = requestInfo.Id, 
                 Text = requestInfo.Id,
                 Node = requestInfo.Node,
                 Url = requestInfo.Url,
                 Milliseconds = requestInfo.Milliseconds,
                 StatusCode = requestInfo.StatusCode,
                 RequestType = requestInfo.RequestType

            }; 

        }
         
    }
}
