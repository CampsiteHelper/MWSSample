﻿using System;
 using System.Collections.Generic;
 using System.Globalization;
 using System.Linq;
 using System.Security.Cryptography;
 using System.Text;
 using System.Xml.Linq;
 using Common;
 using Persistence;

namespace MWSCommon
{
    public class AMZNHelper
    {


       
        string ServiceVersion = "2013-09-01";
        string SignatureVersion = "2";
        string SignatureMethod = "HmacSHA256";
		//string ServiceURL = "https://mws.amazonservices.com/Orders/2013-09-01";
		AmazonAccount _acct;

		private string _AWSAccessKeyId;
		private string _AWSSecretKey;

		string awsAccessKeyId { get
			{
				if (_acct == null)
					return "";
				
				if (String.IsNullOrEmpty(_AWSAccessKeyId))
				{

                 
                    var val = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");

                    if (String.IsNullOrEmpty(val))
					{
                        Log.Error(_acct.AmazonAccountId,"No global access key available!",new ArgumentException());
						return "";
					}
					
					_AWSAccessKeyId = val;
				}
				return _AWSAccessKeyId;

			}
			set{} }


	    private string awsSecretAccessKey
		{
			get
			{

				if (_acct == null)
					return "";

				
				if (String.IsNullOrEmpty(_AWSSecretKey))
				{
					
                    var val = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

                    if (String.IsNullOrEmpty(val))
					{
						Log.Error(_acct.AmazonAccountId, "No global AWSSecretKeyEnc key available!", new ArgumentException());

						return "";
					}
					
					_AWSSecretKey = val;
				}
				return _AWSSecretKey;



			}
			set { }
		}





		public AMZNHelper(AmazonAccount acct)
        {


			_acct = acct;


            
        }



        /**
        * Add authentication related and version parameters
        */
        public void AddRequiredParameters(IDictionary<String, String> parameters, string ServiceURL,bool GET)
        {

			if (!String.IsNullOrEmpty(_acct.AuthToken))
			{

				var MWSAuthToken = _acct.AuthToken;


				parameters["MWSAuthToken"] = MWSAuthToken;
			}


	        if (parameters.ContainsKey("AWSAccessKeyId"))
	        {
		        parameters.Remove("AWSAccessKeyId");
	        }
	        
	        if (parameters.ContainsKey("Timestamp"))
	        {
		        parameters.Remove("Timestamp");
	        }
	        
	        if (parameters.ContainsKey("SignatureVersion"))
	        {
		        parameters.Remove("SignatureVersion");
	        }
	        
	        
	        if (parameters.ContainsKey("Signature"))
	        {
		        parameters.Remove("Signature");
	        }
	        
	        
	        
	        
            parameters.Add("AWSAccessKeyId", awsAccessKeyId);
            parameters.Add("Timestamp", GetFormattedTimestamp(DateTime.Now));
            if(!parameters.Keys.Contains("Version"))
                parameters.Add("Version", ServiceVersion);
      

            parameters.Add("SignatureVersion", SignatureVersion);
            parameters.Add("Signature", SignParameters(parameters, awsSecretAccessKey, ServiceURL,GET));

            if(String.IsNullOrEmpty(parameters["AWSAccessKeyId"]))
            {
                Log.Warn(_acct.AmazonAccountId,"did not find access key id");
            }
        }

        /**
         * Convert Dictionary of paremeters to Url encoded query string
         */
        private string GetParametersAsString(IDictionary<String, String> parameters)
        {
            StringBuilder data = new StringBuilder();
            foreach (String key in parameters.Keys)
            {
                String value = parameters[key];
                if (value != null)
                {
                    data.Append(key);
                    data.Append('=');
                    data.Append(UrlEncode(value, false));
                    data.Append('&');
                }
            }
            String result = data.ToString();
            return result.Remove(result.Length - 1);
        }

        /**
         * Computes RFC 2104-compliant HMAC signature for request parameters
         * Implements AWS Signature, as per following spec:
         *
         * If Signature Version is 0, it signs concatenated Action and Timestamp
         *
         * If Signature Version is 1, it performs the following:
         *
         * Sorts all  parameters (including SignatureVersion and excluding Signature,
         * the value of which is being created), ignoring case.
         *
         * Iterate over the sorted list and append the parameter name (in original case)
         * and then its value. It will not URL-encode the parameter values before
         * constructing this string. There are no separators.
         *
         * If Signature Version is 2, string to sign is based on following:
         *
         *    1. The HTTP Request Method followed by an ASCII newline (%0A)
         *    2. The HTTP Host header in the form of lowercase host, followed by an ASCII newline.
         *    3. The URL encoded HTTP absolute path component of the URI
         *       (up to but not including the query string parameters);
         *       if this is empty use a forward '/'. This parameter is followed by an ASCII newline.
         *    4. The concatenation of all query string components (names and values)
         *       as UTF-8 characters which are URL encoded as per RFC 3986
         *       (hex characters MUST be uppercase), sorted using lexicographic byte ordering.
         *       Parameter names are separated from their values by the '=' character
         *       (ASCII character 61), even if the value is empty.
         *       Pairs of parameter and values are separated by the '&' character (ASCII code 38).
         *
         */
        private String SignParameters(IDictionary<String, String> parameters, String key, string ServiceURL,bool GET)
        {
            String signatureVersion = parameters["SignatureVersion"];

            KeyedHashAlgorithm algorithm = new HMACSHA256();


            String stringToSign = null;
            if ("2".Equals(signatureVersion))
            {
	            
                String signatureMethod = SignatureMethod;
	            if (parameters.ContainsKey("SignatureMethod"))
		            parameters.Remove("SignatureMethod");
	            
                parameters.Add("SignatureMethod", signatureMethod);
                stringToSign = CalculateStringToSignV2(parameters, ServiceURL,GET);
            }
            else
            {
                throw new Exception("Invalid Signature Version specified");
            }

            return Sign(stringToSign, key, algorithm);
        }

        
        private String CalculateStringToSignV2(IDictionary<String, String> parameters, string serviceURL,bool GET)
        {
            StringBuilder data = new StringBuilder();
            IDictionary<String, String> sorted =
                  new SortedDictionary<String, String>(parameters, StringComparer.Ordinal);
            if(GET)
                data.Append("GET");
            else
                data.Append("POST");

            data.Append("\n");
            Uri endpoint = new Uri(serviceURL);

            data.Append(endpoint.Host);
            if (endpoint.Port != 443 && endpoint.Port != 80)
            {
                data.Append(":")
                    .Append(endpoint.Port);
            }
            data.Append("\n");
            String uri = endpoint.AbsolutePath;
            if (uri == null || uri.Length == 0)
            {
                uri = "/";
            }
            data.Append(UrlEncode(uri, true));
            data.Append("\n");
            foreach (KeyValuePair<String, String> pair in sorted)
            {
                if (pair.Value != null)
                {
                    data.Append(UrlEncode(pair.Key, false));
                    data.Append("=");
                    data.Append(UrlEncode(pair.Value, false));
                    data.Append("&");
                }

            }

            String result = data.ToString();
            return result.Remove(result.Length - 1);
        }

        private String UrlEncode(String data, bool path)
        {
            StringBuilder encoded = new StringBuilder();
            String unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~" + (path ? "/" : "");

            foreach (char symbol in Encoding.UTF8.GetBytes(data))
            {
                if (unreservedChars.IndexOf(symbol) != -1)
                {
                    encoded.Append(symbol);
                }
                else
                {
                    encoded.Append("%" + String.Format("{0:X2}", (int)symbol));
                }
            }

            return encoded.ToString();

        }

        /**
         * Computes RFC 2104-compliant HMAC signature.
         */
        private String Sign(String data, String key, KeyedHashAlgorithm algorithm)
        {
            Encoding encoding = new UTF8Encoding();
            algorithm.Key = encoding.GetBytes(key);
            return Convert.ToBase64String(algorithm.ComputeHash(
                encoding.GetBytes(data.ToCharArray())));
        }

        /**
         * Formats date as ISO 8601 timestamp
         */
        public static String GetFormattedTimestamp(DateTime dt)
        {

            if(dt == DateTime.MinValue)
            {
                Log.Warn(0, "GetFormattedTimestamp got a really low date, setting to 10 years ago");
                dt = DateTime.Now.AddYears(-10);
                
            }
            DateTime utcTime;
            if (dt.Kind == DateTimeKind.Local)
            {
                utcTime = new DateTime(
                    dt.Year,
                    dt.Month,
                    dt.Day,
                    dt.Hour,
                    dt.Minute,
                    dt.Second,
                    dt.Millisecond,
                    DateTimeKind.Local).ToUniversalTime();
            }
            else
            {
                // If DateTimeKind.Unspecified, assume UTC.
                utcTime = dt;

            }

            return utcTime.ToString("yyyy-MM-dd\\THH:mm:ss\\Z", CultureInfo.InvariantCulture);
        }


        public string MWSStatus()
        {

            /*
            http://mws.amazonaws.com/FulfillmentInboundShipment/2010-10-01/
  ?AWSAccessKeyId=0PB842EXAMPLE7N4ZTR2
  &Action=GetServiceStatus
  &MWSAuthToken=amzn.mws.4ea38b7b-f563-7709-4bae-87aeaEXAMPLE
  &SellerId=A1XEXAMPLE5E6
  &Signature=ZQLpf8vEXAMPLE0iC265pf18n0%3D
  &SignatureVersion=2
  &SignatureMethod=HmacSHA256
  &Timestamp=2010-11-01T18%3A12%3A21.687Z
  &Version=2010-10-01

    */

			AMZNWebResponse wr = new AMZNWebResponse(_acct);

            IDictionary<string, string> r1 = new Dictionary<string, string>();


            r1["Action"] = "GetServiceStatus";
            r1["SellerId"] = _acct.SellerId;
            r1["Version"] = "2010-10-01";
            String serviceURL = "https://mws.amazonservices.com/FulfillmentInboundShipment";

            DateTime startTime = DateTime.Now;

            string s = wr.getResponse(serviceURL, r1,  false);
            //<TransportStatus>WORKING</TransportStatus>
            int totalMS = (int)(DateTime.Now - startTime).TotalMilliseconds;

            if (String.IsNullOrEmpty(s) || !s.Contains("<Status>"))
            {

                return "DOWN";

            }
            var xDoc = XDocument.Parse(s);



            XElement xe = Util.stripNS(xDoc.Elements().First());

            XElement status = xe.Descendants("Status").First();
            if (status == null)
            {
                return "DOWN";

            }
	        return totalMS + "ms - " + status.Value;
        }


    }
}
