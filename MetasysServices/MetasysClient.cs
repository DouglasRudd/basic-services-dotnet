using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Net;
using System.Collections;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using JohnsonControls.Metasys.BasicServices.Utils;

namespace JohnsonControls.Metasys.BasicServices
{
    /// <summary>
    /// An HTTP client for consuming the most commonly used endpoints of the Metasys API.
    /// </summary>
    public class MetasysClient : BasicServiceProvider, IMetasysClient
    {
        /// <summary>The current session token.</summary>
        protected AccessToken AccessToken;

        /// <summary>The flag used to control automatic session refreshing.</summary>
        protected bool RefreshToken;

        /// <summary>Resource Manager to provide localized translations.</summary>
        protected static System.Resources.ResourceManager Resource = new System.Resources.ResourceManager("JohnsonControls.Metasys.BasicServices.Resources.MetasysResources", typeof(MetasysClient).Assembly);

        /// <summary>Dictionary to provide keys from the commandIdEnumSet.</summary>
        /// <value>Keys as en-US translations, values as the commandIdEnumSet Enumerations.</value>
        protected static Dictionary<string, string> CommandEnumerations;

        /// <summary>Dictionaries to provide keys from the objectTypeEnumSet since there are duplicate keys.</summary>
        /// <value>Keys as en-US translations, values as the objectTypeEnumSet Enumerations.</value>
        protected static List<Dictionary<string, string>> ObjectTypeEnumerations;

        /// <summary>The current Culture Used for Metasys client localization.</summary>
        public CultureInfo Culture { get; set; }

        private string hostname;
        /// <summary>
        /// The hostname of Metasys API server.
        /// </summary>
        protected string Hostname
        {
            get
            {
                return hostname;
            }
            set
            {
                hostname = value;
                // reset the base client according to settings
                initBaseUrl();
                // reset Access Token
                AccessToken = null;
            }
        }

        /// <summary>
        /// An optional boolean flag for ignoring certificate errors by bypassing certificate verification steps.
        /// </summary>
        protected bool IgnoreCertificateErrors { get; private set; }


        private static CultureInfo CultureEnUS = new CultureInfo(1033);

        /// <summary>
        /// Stores retrieved Ids and serves as an in-memory caching layer.
        /// </summary>
        protected Dictionary<string, Guid> IdentifiersDictionary = new Dictionary<string, Guid>();

        /// <summary>
        /// Local instance of Trends service.
        /// </summary>
        public ITrendService Trends { get; set; }

        /// <summary>
        /// Local instance of Alarms service.
        /// </summary>
		public IAlarmsService Alarms { get; set; }

        /// <summary>
        /// Local instance of Audits service.
        /// </summary>
        public IAuditService Audits { get; set; }

        /// <summary>
        /// Initialize the HTTP client with a base URL.    
        /// </summary>
        private void initBaseUrl()
        {            
            if (IgnoreCertificateErrors)
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                HttpClient httpClient = new HttpClient(httpClientHandler);
                httpClient.BaseAddress = new Uri($"https://{hostname}"
                    .AppendPathSegments("api", Version));
                Client = new FlurlClient(httpClient);
            }
            else
            {
                Client = new FlurlClient($"https://{hostname}"
                    .AppendPathSegments("api", Version));
            }
        }

        /// <summary>
        /// Creates a new MetasysClient.
        /// </summary>
        /// <remarks>
        /// Takes an optional boolean flag for ignoring certificate errors by bypassing certificate verification steps.
        /// Verification bypass is not recommended due to security concerns. If your Metasys server is missing a valid certificate it is
        /// recommended to add one to protect your private data from attacks over external networks.
        /// Takes an optional flag for the api version of your Metasys server.
        /// Takes an optional CultureInfo which is useful for formatting numbers and localization of strings. If not specified,
        /// the machine's current culture is used.
        /// Takes an optional flag for logging client errors. If not specified, it is enabled by default according to log4Net.config file.
        /// </remarks>
        /// <param name="hostname">The hostname of the Metasys server.</param>
        /// <param name="ignoreCertificateErrors">Use to bypass server certificate verification.</param>
        /// <param name="version">The server's Api version.</param>
        /// <param name="cultureInfo">Localization culture for Metasys enumeration translations.</param>
        /// <param name="logClientErrors">Set this flag to false to disable logging of client errors.</param>
        public MetasysClient(string hostname, bool ignoreCertificateErrors = false, ApiVersion version = ApiVersion.v2, CultureInfo cultureInfo = null, bool logClientErrors = true)
        {
            Hostname = hostname;
            // Set Metasys culture if specified, otherwise use current machine Culture.
            Culture = cultureInfo ?? CultureInfo.CurrentCulture;
            Version = version;
            IgnoreCertificateErrors = ignoreCertificateErrors;
            initBaseUrl();
            // Set preferences about logging
            LogClientErrors = logClientErrors;
            // Init related services
            Trends = new TrendServiceProvider(Client, Version, logClientErrors);
            Alarms = new AlarmServiceProvider(Client, Version, logClientErrors);
            Audits = new AuditServiceProvider(Client, Version, logClientErrors);
        }

        /// <summary>
        /// Localizes the specified resource key for the current MetasysClient locale or specified culture.
        /// </summary>
        /// <remarks>
        /// The resource parameter must be the key of a Metasys enumeration resource,
        /// otherwise no translation will be found.
        /// </remarks>
        /// <param name="resource">The key for the localization resource.</param>
        /// <param name="cultureInfo">Optional culture specification.</param>
        /// <returns>
        /// Localized string if the resource was found, the default en-US localized string if not found,
        /// or the resource parameter value if neither resource is found.
        /// </returns>
        public string Localize(string resource, CultureInfo cultureInfo = null)
        {
            // Priority is the cultureInfo parameter if available, otherwise MetasysClient culture.
            return Utils.ResourceManager.Localize(resource, cultureInfo ?? Culture);
        }
        /// <summary>
        /// Attempts to get the enumeration key of a given en-US localized command.
        /// </summary>
        /// <remarks>
        /// The resource parameter must be the value of a Metasys commandIdEnumSet en-US value,
        /// otherwise no key will be found.
        /// </remarks>
        /// <param name="resource">The en-US value for the localization resource.</param>
        /// <returns>
        /// The enumeration key of the en-US command if found, original resource if not.
        /// </returns>
        public string GetCommandEnumeration(string resource)
        {
            // Priority is the cultureInfo parameter if available, otherwise MetasysClient culture.
            return Utils.ResourceManager.GetCommandEnumeration(resource);
        }

        /// <summary>
        /// Attempts to get the enumeration key of a given en-US localized objectType.
        /// </summary>
        /// <remarks>
        /// The resource parameter must be the value of a Metasys objectTypeEnumSet en-US value,
        /// otherwise no key will be found.
        /// </remarks>
        /// <param name="resource">The en-US value for the localization resource.</param>
        /// <returns>
        /// The enumeration key of the en-US objectType if found, original resource if not.
        /// </returns>
        public string GetObjectTypeEnumeration(string resource)
        {
            // Priority is the cultureInfo parameter if available, otherwise MetasysClient culture.
            return Utils.ResourceManager.GetObjectTypeEnumeration(resource);
        }

        /// <summary>Use to log an error message from an asynchronous context.</summary>
        private async Task LogErrorAsync(String message)
        {
            await Console.Error.WriteLineAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to login to the given host and retrieve an access token.
        /// </summary>
        /// <returns>Access Token.</returns>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="refresh">Flag to set automatic access token refreshing to keep session active.</param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysTokenException"></exception>
        public AccessToken TryLogin(string username, string password, bool refresh = true)
        {
            return TryLoginAsync(username, password, refresh).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Attempts to login to the given host and retrieve an access token asynchronously.
        /// </summary>
        /// <returns>Asynchronous Task Result as Access Token.</returns>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="refresh">Flag to set automatic access token refreshing to keep session active.</param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysTokenException"></exception>
        public async Task<AccessToken> TryLoginAsync(string username, string password, bool refresh = true)
        {
            this.RefreshToken = refresh;

            try
            {
                var response = await Client.Request("login")
                    .PostJsonAsync(new { username, password })
                    .ReceiveJson<JToken>()
                    .ConfigureAwait(false);

                CreateAccessToken(Hostname, username, response);
            }
            catch (FlurlHttpException e)
            {
                ThrowHttpException(e);
            }
            return this.AccessToken;
        }

        /// <summary>
        /// Requests a new access token from the server before the current token expires.
        /// </summary>
        /// <returns>Access Token.</returns>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysTokenException"></exception>
        public AccessToken Refresh()
        {
            return RefreshAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Requests a new access token from the server before the current token expires asynchronously.
        /// </summary>
        /// <returns>Asynchronous Task Result as Access Token.</returns>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysTokenException"></exception>
        public async Task<AccessToken> RefreshAsync()
        {
            try
            {
                var response = await Client.Request("refreshToken")
                    .GetJsonAsync<JToken>()
                    .ConfigureAwait(false);
                // Since it's a refresh, get issue info from the current token                
                CreateAccessToken(AccessToken.Issuer, AccessToken.IssuedTo, response);
            }
            catch (FlurlHttpException e)
            {
                ThrowHttpException(e);
            }
            return this.AccessToken;
        }

        /// <summary>
        /// Creates a new AccessToken from a JToken and sets the client's authorization header if successful.
        /// On failure leaves the AccessToken and authorization header in previous state.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="issuer"></param>
        /// <param name="issuedTo"></param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysTokenException"></exception>
        private void CreateAccessToken(string issuer, string issuedTo, JToken token)
        {
            try
            {
                var accessTokenValue = token["accessToken"];
                var expires = token["expires"];
                var accessToken = $"Bearer {accessTokenValue.Value<string>()}";
                var date = expires.Value<DateTime>();
                this.AccessToken = new AccessToken(issuer, issuedTo, accessToken, date);
                Client.Headers.Remove("Authorization");
                Client.Headers.Add("Authorization", this.AccessToken.Token);
                if (RefreshToken)
                {
                    ScheduleRefresh();
                }
            }
            catch (Exception e) when (e is System.ArgumentNullException
                || e is System.NullReferenceException || e is System.FormatException)
            {
                throw new MetasysTokenException(token.ToString(), e);
            }
        }

        /// <summary>
        /// Will call Refresh() a minute before the token expires.
        /// </summary>
        private void ScheduleRefresh()
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan delay = AccessToken.Expires - now.AddSeconds(-1); // minimum renew gap of 1 sec in advance
            // Renew one minute before expiration if there is more than one minute time 
            if (delay > new TimeSpan(0, 1, 0))
            {
                delay.Subtract(new TimeSpan(0, 1, 0));
            }
            if (delay <= TimeSpan.Zero)
            {
                // Token already expired
                return;
            }
            int delayms;
            if (delay.TotalMilliseconds > int.MaxValue)
            {
                // Delay is set to int MaxValue to do not go negative with double to int conversion.
                delayms = int.MaxValue;
            }
            else
            {
                delayms = (int)delay.TotalMilliseconds;
            }
            System.Threading.Tasks.Task.Delay(delayms).ContinueWith(_ => Refresh());
        }

        /// <summary>
        /// Returns the current session access token.
        /// </summary>
        /// <returns>Current session's Access Token.</returns>
        public AccessToken GetAccessToken()
        {
            return this.AccessToken;
        }

        /// <summary>
        /// Set the current session access token.
        /// </summary>
        /// <returns>Current session's Access Token.</returns>
        public void SetAccessToken(AccessToken accessToken)
        {
            this.AccessToken = accessToken;
        }

        /// <summary>
        /// Given the Item Reference of an object, returns the object identifier.
        /// </summary>
        /// <remarks>
        /// The itemReference will be automatically URL encoded.
        /// </remarks>
        /// <returns>A Guid representing the id, or an empty Guid if errors occurred.</returns>
        /// <param name="itemReference"></param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysGuidException"></exception>
        public Guid GetObjectIdentifier(string itemReference)
        {
            return GetObjectIdentifierAsync(itemReference).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Given the Item Reference of an object, returns the object identifier asynchronously.
        /// </summary>
        /// <remarks>
        /// The itemReference will be automatically URL encoded. 
        /// For repeated requests will be returned the in-line value.
        /// </remarks>
        /// <param name="itemReference"></param>
        /// <returns>
        /// Asynchronous Task Result as a Guid representing the id, or an empty Guid if errors occurred.
        /// </returns>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysGuidException"></exception>
        public async Task<Guid> GetObjectIdentifierAsync(string itemReference)
        {
            // Sanitize given itemReference
            var normalizedItemReference = itemReference.Trim().ToUpper();
            // Returns cached value when available, otherwise perform request         
            if (!IdentifiersDictionary.ContainsKey(normalizedItemReference))
            {
                JToken response = null;
                try
                {
                    response = await Client.Request("objectIdentifiers")
                        .SetQueryParam("fqr", itemReference)
                        .GetJsonAsync<JToken>()
                        .ConfigureAwait(false);
                    // Stores value for caching and return
                    IdentifiersDictionary[normalizedItemReference] = ParseObjectIdentifier(response);
                }
                catch (FlurlHttpException e)
                {
                    if (e.Call.HttpStatus == HttpStatusCode.BadRequest && response != null && response["message"] != null
                        && response["message"].Value<string>().Contains("not found"))
                    {
                        // Metasys respond with "Identifier is not found." message, so make exception explicit
                        throw new MetasysHttpNotFoundException(e);
                    }
                    ThrowHttpException(e);
                }
            }
            return IdentifiersDictionary[normalizedItemReference];
        }

        /// <summary>
        /// Read one attribute value given the Guid of the object.
        /// </summary>
        /// <returns>
        /// Variant if the attribute exists, null if does not exist.
        /// </returns>
        /// <param name="id"></param>
        /// <param name="attributeName"></param>        
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysHttpTimeoutException"></exception>
        /// <exception cref="MetasysHttpParsingException"></exception>
        /// <exception cref="MetasysHttpNotFoundException"></exception>
        /// <exception cref="MetasysPropertyException"></exception>
        public Variant ReadProperty(Guid id, string attributeName)
        {
            return ReadPropertyAsync(id, attributeName).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read one attribute value given the Guid of the object asynchronously.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="attributeName"></param>      
        /// <returns>
        /// Asynchronous Task Result as Variant if the attribute exists, null if does not exist.
        /// </returns>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysHttpTimeoutException"></exception>
        /// <exception cref="MetasysHttpParsingException"></exception>
        /// <exception cref="MetasysHttpNotFoundException"></exception>
        /// <exception cref="MetasysPropertyException"></exception>
        public async Task<Variant> ReadPropertyAsync(Guid id, string attributeName)
        {
            JToken response = null; Variant result = new Variant();
            try
            {
                response = await Client.Request(new Url("objects")
                    .AppendPathSegments(id, "attributes", attributeName))
                    .GetJsonAsync<JToken>()
                    .ConfigureAwait(false);
                result = new Variant(id, response, attributeName, Culture, Version);
            }
            catch (FlurlHttpException e)
            {
                ThrowHttpException(e);
            }
            catch (System.NullReferenceException e)
            {
                throw new MetasysPropertyException(response.ToString(), e);
            }
            return result;
        }

        /// <summary>
        /// Overload of ReadPropertyAsync for internal use where Exception suppress is needed, e.g. ReadPropertyMultiple 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="attributeName"></param>
        /// <param name="suppressNotFoundException"></param>
        /// <returns></returns>
        protected async Task<Variant> ReadPropertyAsync(Guid id, string attributeName, bool suppressNotFoundException = true)
        {
            try
            {
                return await ReadPropertyAsync(id, attributeName).ConfigureAwait(false);
            }
            catch (MetasysHttpNotFoundException) when (suppressNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Read many attribute values given the Guids of the objects.
        /// </summary>
        /// <returns>
        /// A list of VariantMultiple with all the specified attributes (if existing).        
        /// </returns>
        /// <param name="ids"></param>
        /// <param name="attributeNames"></param>        
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysPropertyException"></exception>
        public IEnumerable<VariantMultiple> ReadPropertyMultiple(IEnumerable<Guid> ids,
        IEnumerable<string> attributeNames)
        {
            return ReadPropertyMultipleAsync(ids, attributeNames).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read many attribute values given the Guids of the objects asynchronously.
        /// </summary>
        /// <remarks>
        /// In order to allow method to run to completion without error, this will ignore properties that do not exist on a given object.         
        /// </remarks>
        /// <returns>
        /// Asynchronous Task Result as list of VariantMultiple with all the specified attributes (if existing).
        /// </returns>
        /// <param name="ids"></param>
        /// <param name="attributeNames"></param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysPropertyException"></exception>
        public async Task<IEnumerable<VariantMultiple>> ReadPropertyMultipleAsync(IEnumerable<Guid> ids,
            IEnumerable<string> attributeNames)
        {
            if (ids == null || attributeNames == null)
            {
                return null;
            }
            List<VariantMultiple> results = new List<VariantMultiple>();
            if (Version > ApiVersion.v2)
            {
                var response = await PostBatchRequestAsync("objects", ids, attributeNames, "attributes").ConfigureAwait(false);
                return ToVariantMultiples(response);
            }
            var taskList = new List<Task<Variant>>();
            // Prepare Tasks to Read attributes list. In Metasys 11 this will be implemented server side
            foreach (var id in ids)
            {
                foreach (string attributeName in attributeNames)
                {
                    // Much faster reading single property than the entire object, even though we have more server calls
                    taskList.Add(ReadPropertyAsync(id, attributeName, true)); // Using internal signature with exception suppress
                }
            }
            await Task.WhenAll(taskList).ConfigureAwait(false);
            foreach (var id in ids)
            {
                // Get attributes of the specific Id
                List<Task<Variant>> attributeList = taskList.Where(w =>
                    (w.Result != null && w.Result.Id == id)).ToList();
                List<Variant> variants = new List<Variant>();
                foreach (var t in attributeList)
                {
                    if (t.Result != null) // Something went wrong if the result is unknown
                    {
                        variants.Add(t.Result); // Prepare variants list
                    }
                }
                if (variants.Count > 0 || attributeNames.Count() == 0)
                {
                    // Aggregate results only when objects was found or no attributes specified
                    results.Add(new VariantMultiple(id, variants));
                }
            }
            return results.AsEnumerable();
        }

        /// <summary>
        /// Write a single attribute given the Guid of the object. 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="attributeName"></param>
        /// <param name="newValue"></param>
        /// <param name="priority">Write priority as an enumeration from the writePriorityEnumSet.</param>
        /// <exception cref="MetasysHttpException"></exception>
        public void WriteProperty(Guid id, string attributeName, object newValue, string priority = null)
        {
            WritePropertyAsync(id, attributeName, newValue, priority).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Write a single attribute given the Guid of the object asynchronously.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="attributeName"></param>
        /// <param name="newValue"></param>
        /// <param name="priority">Write priority as an enumeration from the writePriorityEnumSet.</param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <returns>Asynchronous Task Result.</returns>
        public async Task WritePropertyAsync(Guid id, string attributeName, object newValue, string priority = null)
        {
            List<(string Attribute, object Value)> list = new List<(string Attribute, object Value)>();
            list.Add((Attribute: attributeName, Value: newValue));
            var item = GetWritePropertyBody(list, priority);
            await WritePropertyRequestAsync(id, item).ConfigureAwait(false);
        }

        /// <summary>
        /// Write to many attribute values given the Guids of the objects.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="attributeValues">The (attribute, value) pairs.</param>
        /// <param name="priority">Write priority as an enumeration from the writePriorityEnumSet.</param>
        /// <exception cref="MetasysHttpException"></exception>
        public void WritePropertyMultiple(IEnumerable<Guid> ids,
            IEnumerable<(string Attribute, object Value)> attributeValues, string priority = null)
        {
            WritePropertyMultipleAsync(ids, attributeValues, priority).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Write to many attribute values given the Guids of the objects asynchronously.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="attributeValues">The (attribute, value) pairs.</param>
        /// <param name="priority">Write priority as an enumeration from the writePriorityEnumSet.</param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <returns>Asynchronous Task Result.</returns>
        public async Task WritePropertyMultipleAsync(IEnumerable<Guid> ids,
            IEnumerable<(string Attribute, object Value)> attributeValues, string priority = null)
        {
            if (ids == null || attributeValues == null)
            {
                return;
            }

            var item = GetWritePropertyBody(attributeValues, priority);
            var taskList = new List<Task>();

            foreach (var id in ids)
            {
                taskList.Add(WritePropertyRequestAsync(id, item));
            }
            await Task.WhenAll(taskList).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates the body for the WriteProperty and WritePropertyMultiple requests as a dictionary.
        /// </summary>
        /// <param name="attributeValues">The (attribute, value) pairs.</param>
        /// <param name="priority">Write priority as an enumeration from the writePriorityEnumSet.</param>
        /// <returns>Dictionary of the attribute, value pairs.</returns>
        private Dictionary<string, object> GetWritePropertyBody(
            IEnumerable<(string Attribute, object Value)> attributeValues, string priority)
        {
            Dictionary<string, object> pairs = new Dictionary<string, object>();
            foreach (var attribute in attributeValues)
            {
                pairs.Add(attribute.Attribute, attribute.Value);
            }

            if (priority != null)
            {
                pairs.Add("priority", priority);
            }

            return pairs;
        }

        /// <summary>
        /// Write one or many attribute values in the provided json given the Guid of the object asynchronously.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="body"></param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <returns>Asynchronous Task Result.</returns>
        private async Task WritePropertyRequestAsync(Guid id, Dictionary<string, object> body)
        {
            var json = new { item = body };

            try
            {
                var response = await Client.Request(new Url("objects")
                    .AppendPathSegment(id))
                    .PatchJsonAsync(json)
                    .ConfigureAwait(false);
            }
            catch (FlurlHttpException e)
            {
                ThrowHttpException(e);
            }
        }

        /// <summary>
        /// Get all available commands given the Guid of the object.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>List of Commands.</returns>
        public IEnumerable<Command> GetCommands(Guid id)
        {
            return GetCommandsAsync(id).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get all available commands given the Guid of the object asynchronously.
        /// </summary>
        /// <param name="id"></param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <returns>Asynchronous Task Result as list of Commands.</returns>
        public async Task<IEnumerable<Command>> GetCommandsAsync(Guid id)
        {
            try
            {
                var token = await Client.Request(new Url("objects")
                    .AppendPathSegments(id, "commands"))
                    .GetJsonAsync<JToken>()
                    .ConfigureAwait(false);

                List<Command> commands = new List<Command>();
                var array = token as JArray;

                if (array != null)
                {
                    foreach (JObject command in array)
                    {
                        try
                        {
                            Command c = new Command(command, Culture);
                            commands.Add(c);
                        }
                        catch (Exception e)
                        {
                            throw new MetasysCommandException(command.ToString(), e);
                        }
                    }
                }

                return commands;
            }
            catch (FlurlHttpException e)
            {
                ThrowHttpException(e);
            }
            return null;
        }

        /// <summary>
        /// Send a command to an object.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="command"></param>
        /// <param name="values"></param>
        /// <exception cref="MetasysHttpException"></exception>
        public void SendCommand(Guid id, string command, IEnumerable<object> values = null)
        {
            SendCommandAsync(id, command, values).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Send a command to an object asynchronously.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="command"></param>
        /// <param name="values"></param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <returns>Asynchronous Task Result.</returns>
        public async Task SendCommandAsync(Guid id, string command, IEnumerable<object> values = null)
        {
            if (values == null)
            {
                await SendCommandRequestAsync(id, command, new string[0]).ConfigureAwait(false);
            }
            else
            {
                await SendCommandRequestAsync(id, command, values).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Send a command to an object asynchronously.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="command"></param>
        /// <param name="values"></param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <returns>Asynchronous Task Result.</returns>
        private async Task SendCommandRequestAsync(Guid id, string command, IEnumerable<object> values)
        {
            try
            {
                var response = await Client.Request(new Url("objects")
                    .AppendPathSegments(id, "commands", command))
                    .PutJsonAsync(values)
                    .ConfigureAwait(false);
            }
            catch (FlurlHttpException e)
            {
                ThrowHttpException(e);
            }
        }

        /// <summary>
        /// Gets all network devices.
        /// </summary>
        /// <param name="type">Optional type number as a string</param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysHttpParsingException"></exception>
        public IEnumerable<MetasysObject> GetNetworkDevices(string type = null)
        {
            return GetNetworkDevicesAsync(type).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets all network devices asynchronously by requesting each available page.
        /// </summary>
        /// <param name="type">Optional type number as a string</param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysHttpParsingException"></exception>
        public async Task<IEnumerable<MetasysObject>> GetNetworkDevicesAsync(string type = null)
        {
            var response = await this.GetAllAvailablePagesAsync("networkDevices", new Dictionary<string, string> { { "type", type } }).ConfigureAwait(false);
            return toMetasysObject(response);
        }


        /// <summary>
        /// Gets all available network device types.
        /// </summary>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysHttpParsingException"></exception>
        public IEnumerable<MetasysObjectType> GetNetworkDeviceTypes()
        {
            return GetNetworkDeviceTypesAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets all available network device types asynchronously.
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<MetasysObjectType>> GetNetworkDeviceTypesAsync()
        {
            return await GetResourceTypesAsync("networkDevices", "availableTypes").ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all resource types asynchronously.
        /// </summary>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysHttpParsingException"></exception>
        public async Task<IEnumerable<MetasysObjectType>> GetResourceTypesAsync(string resource, string pathSegment)
        {
            List<MetasysObjectType> types = new List<MetasysObjectType>() { };

            try
            {
                var response = await Client.Request(new Url(resource)
                    .AppendPathSegment(pathSegment))
                    .GetJsonAsync<JToken>()
                    .ConfigureAwait(false);
                try
                {
                    // The response is a list of typeUrls, not the type data
                    var list = response["items"] as JArray;
                    foreach (var item in list)
                    {
                        try
                        {
                            JToken typeToken = item;
                            // Retrieve type token from url (when available) and construct Metasys Object Type
                            if (item["typeUrl"] != null)
                            {
                                var url = item["typeUrl"].Value<string>();
                                typeToken = await GetWithFullUrl(url).ConfigureAwait(false);
                            }
                            var type = GetType(typeToken);
                            types.Add(type);
                        }
                        catch (System.ArgumentNullException e)
                        {
                            throw new MetasysHttpParsingException(response.ToString(), e);
                        }
                    }
                }
                catch (System.NullReferenceException e)
                {
                    throw new MetasysHttpParsingException(response.ToString(), e);
                }
            }
            catch (FlurlHttpException e)
            {
                ThrowHttpException(e);
            }
            return types;
        }

        /// <summary>
        /// Gets the type from a token retrieved from a typeUrl 
        /// </summary>
        /// <param name="typeToken"></param>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysObjectTypeException"></exception>
        protected MetasysObjectType GetType(JToken typeToken)
        {
            try
            {
                string description = typeToken["description"].Value<string>();
                int id = typeToken["id"].Value<int>();
                string key = GetObjectTypeEnumeration(description);
                string translation = Localize(key);
                if (translation != key)
                {
                    // A translation was found
                    description = translation;
                }
                return new MetasysObjectType(id, key, description);
            }
            catch (Exception e) when (e is System.ArgumentNullException
                || e is System.NullReferenceException || e is System.FormatException)
            {
                throw new MetasysObjectTypeException(typeToken.ToString(), e);
            }
        }

        /// <summary>
        /// Gets all child objects given a parent Guid.
        /// Level indicates how deep to retrieve objects.
        /// </summary>
        /// <remarks>
        /// A level of 1 only retrieves immediate children of the parent object.
        /// </remarks>
        /// <param name="id"></param>
        /// <param name="levels">The depth of the children to retrieve.</param>      
        /// <param name="includeInternalObjects">Set it to true to see also internal objects that are not displayed in the Metasys tree. </param>      
        /// <remarks> The flag includeInternalObjects applies since Metasys API v3. </remarks>
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysHttpParsingException"></exception>
        public IEnumerable<MetasysObject> GetObjects(Guid id, int levels = 1, bool includeInternalObjects = false)
        {
            return GetObjectsAsync(id, levels, includeInternalObjects).GetAwaiter().GetResult();
        }


        /// <summary>
        /// Gets all spaces by requesting each available page.
        /// </summary>
        /// <param name="type">Optional type ID belonging to SpaceTypeEnum.</param>            
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysHttpParsingException"></exception>
        public IEnumerable<MetasysObject> GetSpaces(SpaceTypeEnum? type = null)
        {
            return GetSpacesAsync(type).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets all spaces asynchronously by requesting each available page.
        /// </summary>
        /// <param name="type">Optional type ID belonging to SpaceTypeEnum.</param>                
        /// <exception cref="MetasysHttpException"></exception>
        /// <exception cref="MetasysHttpParsingException"></exception>
        public async Task<IEnumerable<MetasysObject>> GetSpacesAsync(SpaceTypeEnum? type = null)
        {
            Dictionary<string, string> parameters = null;
            if (type != null)
            {
                // Init Dictionary with Space Type parameter
                parameters = new Dictionary<string, string>() { { "type", ((int)type).ToString() } };
            }
            var spaces = await GetAllAvailablePagesAsync("spaces", parameters).ConfigureAwait(false);
            return toMetasysObject(spaces, type: MetasysObjectTypeEnum.Space);
        }

        /// <summary>
        /// Gets children spaces of the given space.
        /// </summary>
        /// <param name="id">The GUID of the parent space.</param>   
        public IEnumerable<MetasysObject> GetSpaceChildren(Guid id)
        {
            return GetSpaceChildrenAsync(id).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets children spaces of the given space asynchronously.
        /// </summary>
        /// <param name="id">The GUID of the parent space.</param>      
        public async Task<IEnumerable<MetasysObject>> GetSpaceChildrenAsync(Guid id)
        {
            var spaceChildren = await GetAllAvailablePagesAsync("spaces", null, id.ToString(), "spaces").ConfigureAwait(false);
            return toMetasysObject(spaceChildren, MetasysObjectTypeEnum.Space);
        }


        /// <summary>
        /// Gets all equipment by requesting each available page.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<MetasysObject> GetEquipment()
        {
            return GetEquipmentAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        ///  Gets all equipment asynchronously by requesting each available page.
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<MetasysObject>> GetEquipmentAsync()
        {
            var equipment = await GetAllAvailablePagesAsync("equipment").ConfigureAwait(false);
            return toMetasysObject(equipment, MetasysObjectTypeEnum.Equipment);
        }

        /// <summary>
        /// Gets all space types.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<MetasysObjectType> GetSpaceTypes()
        {
            return GetSpaceTypesAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets all space types asynchronously.
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<MetasysObjectType>> GetSpaceTypesAsync()
        {
            return await GetResourceTypesAsync("enumSets", "1766/members").ConfigureAwait(false);
        }

        /// <summary>
        ///  Gets all Equipment for the given space
        /// </summary>
        /// <param name="spaceId"></param>
        /// <returns></returns>
        public IEnumerable<MetasysObject> GetSpaceEquipment(Guid spaceId)
        {
            return GetSpaceEquipmentAsync(spaceId).GetAwaiter().GetResult();
        }

        /// <summary>
        ///  Gets all Equipment for the given space asynchronously
        /// </summary>
        /// <param name="spaceId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<MetasysObject>> GetSpaceEquipmentAsync(Guid spaceId)
        {
            var spaceEquipment = await GetAllAvailablePagesAsync("spaces", null, spaceId.ToString(), "equipment").ConfigureAwait(false);
            return toMetasysObject(spaceEquipment, MetasysObjectTypeEnum.Equipment);
        }

        /// <summary>
        /// Gets all points for the given Equipment.
        /// </summary>
        /// <param name="equipmentId">The Guid of the equipment.</param>
        /// <param name="readAttributeValue">Set to false if you would not read Points Attribute Value.</param>
        /// <remarks> Reading the Attribute Value attribute could take time depending on the number of points. </remarks>
        /// <returns></returns>
        public IEnumerable<MetasysPoint> GetEquipmentPoints(Guid equipmentId, bool readAttributeValue = true)
        {
            return GetEquipmentPointsAsync(equipmentId, readAttributeValue).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets all points for the given Equipment asyncronously.
        /// </summary>
        /// <remarks> Returned Points List contains PresentValue when available. </remarks>
        /// <param name="equipmentId">The Guid of the equipment.</param>
        /// <param name="readAttributeValue">Set to false if you would not read Points Attribute Value.</param>
        /// <remarks> Reading the Attribute Value could take time depending on the number of points. </remarks>
        /// <returns></returns>
        public async Task<IEnumerable<MetasysPoint>> GetEquipmentPointsAsync(Guid equipmentId, bool readAttributeValue = true)
        {
            List<MetasysPoint> points = new List<MetasysPoint>() { }; List<Guid> guids = new List<Guid>();
            List<MetasysPoint> pointsWithAttribute = new List<MetasysPoint>() { };
            var response = await GetAllAvailablePagesAsync("equipment", null, equipmentId.ToString(), "points").ConfigureAwait(false);
            try
            {
                foreach (var item in response)
                {
                    MetasysPoint point = new MetasysPoint(item);
                    // Retrieve object Id from full URL 
                    string objectId = point.ObjectUrl.Split('/').Last();
                    point.ObjectId = ParseObjectIdentifier(objectId);
                    // Retrieve attribute Id from full URL 
                    string attributeId = point.AttributeUrl.Split('/').Last();
                    if (point.ObjectId != Guid.Empty) // Sometime can happen that there are empty Guids.
                    {
                        // Collect Guids to perform read property multiple in "one call" (supporting only presentValue so far)
                        if (attributeId == "85" && readAttributeValue)
                        {
                            guids.Add(point.ObjectId);
                            pointsWithAttribute.Add(point);
                        }
                        else
                        {
                            points.Add(point);
                        }
                    }
                }
            }
            catch (System.NullReferenceException e)
            {
                throw new MetasysHttpParsingException(response.ToString(), e);
            }
            if (readAttributeValue)
            {
                // Try to Read Present Value when available. Note: can't read attribute ID from attribute full URL of point since we got only the description.
                var results = await ReadPropertyMultipleAsync(guids, new List<string> { "presentValue" }).ConfigureAwait(false);
                foreach (var r in results)
                {
                    var point = pointsWithAttribute.SingleOrDefault(s => s.ObjectId == r.Id);
                    // Assign present values back from the attribute collection
                    point.PresentValue = r.Values?.SingleOrDefault(s => s.Attribute == "presentValue");
                    // Finally add the point back to the original list
                    points.Add(point);
                }
            }
            return points;
        }

        /// <inheritdoc cref="GetObjects(Guid, int)"/>
        public async Task<IEnumerable<MetasysObject>> GetObjectsAsync(Guid id, int levels, bool includeInternalObjects = false)
        {
            Dictionary<string, string> parameters = null;
            if (Version > ApiVersion.v2)
            {
                // Since API v3 we could use the includeInternalObjects parameter
                parameters = new Dictionary<string, string>();
                parameters.Add("includeInternalObjects", includeInternalObjects.ToString());
            }
            var objects = await GetObjectChildrenAsync(id, parameters, levels).ConfigureAwait(false);
            return toMetasysObject(objects);
        }

        /// <summary>
        /// Attempts to login to the given host using Credential Manager and retrieve an access token.
        /// </summary>
        /// <param name="credManTarget">The Credential Manager target where to pick the credentials.</param>
        /// <param name="refresh">Flag to set automatic access token refreshing to keep session active.</param>
        /// <remarks> This method can be overridden by extended class with other Credential Manager implementations. </remarks>
        public virtual AccessToken TryLogin(string credManTarget, bool refresh = true)
        {
            return TryLoginAsync(credManTarget, refresh).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Attempts to login to the given host using Credential Manager and retrieve an access token asynchronously.
        /// </summary>
        /// <param name="credManTarget">The Credential Manager target where to pick the credentials.</param>
        /// <param name="refresh">Flag to set automatic access token refreshing to keep session active.</param>
        /// <remarks> This method can be overridden by extended class with other Credential Manager implementations. </remarks>
        public virtual async Task<AccessToken> TryLoginAsync(string credManTarget, bool refresh = true)
        {
            // Retrieve credentials first
            var credentials = CredentialUtil.GetCredential(credManTarget);
            // Get the control back to TryLogin method
            return await TryLoginAsync(CredentialUtil.convertToUnSecureString(credentials.Username), CredentialUtil.convertToUnSecureString(credentials.Password), refresh).ConfigureAwait(false);
        }

        /// <summary>
        /// Convert a JToken batch request response into VariantMultiple.
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        protected IEnumerable<VariantMultiple> ToVariantMultiples(JToken response)
        {
            List<VariantMultiple> multiples = new List<VariantMultiple>();
            foreach (var r in response["responses"])
            {
                var respIds = r["id"].Value<string>().Split('_');
                var objId = new Guid(respIds[0]);
                string attr = respIds[1];
                List<Variant> values = new List<Variant>();
                if (r["status"].Value<int>() == 200)
                {
                    values.Add(new Variant(objId, r["body"], attr, Culture, Version));
                } // Don't add the variant to the list if the response is not successful
                var m = multiples.SingleOrDefault(s => s.Id == objId);
                if (m == null)
                {
                    // Add a new multiple for the current object                   
                    multiples.Add(new VariantMultiple(objId, values));
                }
                else
                {
                    // Variant multiple already exists, just add Values
                    var newList = m.Values.ToList();
                    newList.AddRange(values);
                    m.Values = newList;
                }
            }
            return multiples;
        }
    }
}
