# TODO

1. Remove warning LWX008: this is unecessay.
2. Move Lwx.Builder.MicroService.Tests/Projects/MicroService Endpoints and Workers to Lwx.Builder.MicroService.Tests and update the disabled tests to use it.
3. Locate and remove dangling ocurrences of ServiceConfig (it was renamed to Service).
4. Update the Lwx.Builders.MicroService to provide a /health and /ready endpoints. The health endpoint must wait the Lifecycle start event before returning (use a TaskCompletionSource) to ensure the first answer will only come after the server started. 
5. Make Lwx.Builders.MicroService initialization to list all endpoints, workers and other resources on the console. The Endpoints should be listed as method path?query -> Endpoint class. The workers should be listed as Worker class nThreads=x