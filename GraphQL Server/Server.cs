using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

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

        public GraphQLServer(int port = 8080)
        {
            _httpListener = new HttpListener();
            // Allow the server to listen on any address, with https and http, on a port
            _httpListener.Prefixes.Add($"http://+:{port}/");

            // Set up request handle thread
            _handleThread = new Thread(HandleRequests);
        }

        /// <summary>
        /// Starts the graphql Server
        /// </summary>
        public void Start()
        {
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
                controller.server = this;
                _controllers.Add(controller);
            }
        }

        /// <summary>
        /// Add multiple controllers to a server
        /// </summary>
        /// <param name="controllerList"> List of controllers to add</param>
        public void AddController(IEnumerable<GraphQLController> controllerList)
        {
            lock (_controllers.SyncRoot)
            {
                foreach (var controller in controllerList)
                {
                    controller.server = this;
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
                controller.server = null;
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
                ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessGraphQLRequest), ctx);
            }
        }

        private static void ProcessGraphQLRequest(Object arg)
        {
            GraphQLContext ctx = (GraphQLContext) arg;
            var body = new StreamReader(ctx.HttpContext.Request.InputStream).ReadToEnd();
            Console.WriteLine(ctx.HttpContext.Request);
        }
    }
}