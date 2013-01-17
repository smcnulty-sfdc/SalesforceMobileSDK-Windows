﻿using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Salesforce.WinSDK.Net
{
    public class HttpCall
    {
        private readonly String _url;
        private readonly String _method;
        private readonly String _requestBody;

        private ManualResetEvent _allDone;
        private HttpWebRequest _request;
        private HttpWebResponse _response;
        private String _responseBody;

        public String ResponseBody
        {
            get
            {
                if (_response == null)
                {
                    throw new System.InvalidOperationException("HttpCall must be executed first");
                }

                if (_responseBody == null)
                {
                    using (var reader = new StreamReader(_response.GetResponseStream()))
                    {
                        _responseBody = reader.ReadToEnd();
                    }
                }
                return _responseBody;
            }
        }

        public HttpStatusCode StatusCode
        {
            get
            {
                if (_response == null)
                {
                    throw new System.InvalidOperationException("HttpCall must be executed first");
                }

                return _response.StatusCode;
            }
        }

        private HttpCall(String url, String requestBody, String method)
        {
            _url = url;
            _requestBody = requestBody;
            _method = method;
        }

        public static HttpCall createGet(String url) 
        {
            return new HttpCall(url, null, "GET");
        }

        public static HttpCall createPost(String url, String requestBody)
        {
            return new HttpCall(url, requestBody, "POST");
        }

        public async Task<HttpCall> execute()
        {
            return await Task.Factory.StartNew(() => executeSync());
        }

        private HttpCall executeSync() 
        {
            if (_allDone != null)
            {
                throw new System.InvalidOperationException("A HttpCall can only be executed once");
            }

            _allDone = new ManualResetEvent(false);
            _request = (HttpWebRequest)HttpWebRequest.Create(_url);
            _request.Method = _method;

            if (_method == "GET")
            {
                // Start the asynchronous operation to get the response
                _request.BeginGetResponse(new AsyncCallback(GetResponseCallback), null);
            }
            else if (_method == "POST")
            {
                // Start the asynchronous operation to send the request body
                _request.BeginGetRequestStream(new AsyncCallback(GetRequestStreamCallback), null);
            }

            // Keep the thread from continuing while the asynchronous 
            _allDone.WaitOne();

            return this;
        }

        private void GetRequestStreamCallback(IAsyncResult asynchronousResult)
        {
            // End the operation
            Stream postStream = _request.EndGetRequestStream(asynchronousResult);

            // Sending post body
            using(var writer = new StreamWriter(postStream))
            {
                writer.Write(_requestBody);
            }

            // Start the asynchronous operation to get the response
            _request.BeginGetResponse(new AsyncCallback(GetResponseCallback), null);
        }

        private void GetResponseCallback(IAsyncResult asynchronousResult)
        {
            // End the operation
            try
            {
                _response = (HttpWebResponse)_request.EndGetResponse(asynchronousResult);
            }
            catch (System.Net.WebException ex)
            {
                _response = (HttpWebResponse)ex.Response;
            }

            // Signalling that we are done
            _allDone.Set();
        }
    }
}
