using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GraphQL_Server
{
    public class ClientContext
    {
        public bool Authenticated { get; set; }
        public string UserToken { get; set; }
    }

    public class GraphQLContext
    {
        public HttpListenerContext HttpContext { get; set; }
        public ClientContext Client { get; set; }
    }


    public class GraphQLServer
    {
        private HttpListener _httpListener;
        private Thread _handleThread;

        private readonly SynchronizedCollection<GraphQLController> _controllers =
            new SynchronizedCollection<GraphQLController>();

        private static ClientContext DefaultClientContextFn(GraphQLServer x, HttpListenerRequest y) =>
            new ClientContext() {Authenticated = false, UserToken = ""};

        private Func<GraphQLServer, HttpListenerRequest, ClientContext> _clientContextFunc = DefaultClientContextFn;

        private InteractiveSchema _schema;

        public GraphQLServer(int port = 8080)
        {
            _httpListener = new HttpListener();
            // Allow the server to listen on any address, with https and http, on a port
            _httpListener.Prefixes.Add($"http://+:{port}/");

            // Set up request handle thread
            _handleThread = new Thread(HandleRequests);

            // Set up types
            try
            {
                GraphQLType.RegisterIntrinsic();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Starts the graphql Server
        /// </summary>
        public void Start()
        {
            lock (_controllers.SyncRoot)
            {
                _schema = new InteractiveSchema(_controllers);
            }

            _httpListener.Start();
            _handleThread.Start();

            Console.WriteLine("Listening!");
        }

        /// <summary>
        /// Stop the graphql Server
        /// </summary>
        public void Stop()
        {
            _httpListener.Stop();
            _handleThread.Join();
        }

        /// <summary>
        /// Add a model controller to the server
        /// </summary>
        /// <param name="controller"> The controller to add to the server</param>
        public void AddController(GraphQLController controller)
        {
            lock (_controllers.SyncRoot)
            {
                _controllers.Add(controller);
            }
        }

        /// <summary>
        /// Add multiple controllers to a server
        /// </summary>
        /// <param name="controllerList"> List of controllers to add</param>
        public void AddControllers(IEnumerable<GraphQLController> controllerList)
        {
            lock (_controllers.SyncRoot)
            {
                foreach (var controller in controllerList)
                {
                    _controllers.Add(controller);
                }
            }
        }


        /// <summary>
        /// Remove a controller from the server
        /// </summary>
        /// <param name="controller">the controller to remove</param>
        public void RemoveController(GraphQLController controller)
        {
            lock (_controllers.SyncRoot)
            {
                _controllers.Remove(controller);
            }
        }

        public void SetClientContextFunction(Func<GraphQLServer, HttpListenerRequest, ClientContext> contextFunc)
        {
            lock (_clientContextFunc)
            {
                _clientContextFunc = contextFunc;
            }
        }


        /// <summary>
        /// Internal request handler that will deligate the requests to other threads. This has to be in it's own thread
        /// or it will interupt the thread (it has a while loop)
        /// </summary>
        private void HandleRequests()
        {
            while (_httpListener.IsListening)
            {
                var context = _httpListener.GetContext();
                ClientContext clientContext = null;
                lock (_clientContextFunc)
                {
                    clientContext = _clientContextFunc(this, context.Request);
                }


                GraphQLContext ctx = new GraphQLContext() {HttpContext = context, Client = clientContext};

                ThreadPool.QueueUserWorkItem(ProcessGraphQLRequest, ctx);
            }
        }

        private void ProcessGraphQLRequest(Object arg)
        {
            try
            {
                GraphQLContext ctx = (GraphQLContext) arg;
                var body = new StreamReader(ctx.HttpContext.Request.InputStream).ReadToEnd();
                JArray errors = JArray.FromObject(new Object[] { });
                var data = new JObject();
                try
                {
                    var parsed = Parser.ParseRequest(body);

                    lock (_schema)
                    {
                        foreach (var node in parsed)
                        {
                            var resp = _schema.Call(node);
                            if (resp != null)
                                data.Merge(resp);
                        }
                    }
                }
                catch (Exception e)
                {
                    errors.Add(JObject.FromObject(new {message = e.Message, stack = e.StackTrace}));
                }

                var toSerialize = new JObject();
                toSerialize["data"] = data;
                if (errors.Count > 0)
                    toSerialize["errors"] = errors;
                var serialized = JsonConvert.SerializeObject(toSerialize);

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(serialized);
                var response = ctx.HttpContext.Response;
                response.StatusCode = errors.Count == 0 ? 200 : 206;
                response.ContentLength64 = buffer.Length;
                response.Headers.Add("Content-Type", "application/json");
                var outputStream = response.OutputStream;
                outputStream.Write(buffer, 0, buffer.Length);
                outputStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}