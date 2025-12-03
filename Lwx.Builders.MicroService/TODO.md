# TODO

1. Remove warning LWX008: this is unecessay. (DONE - removed informational diagnostic and tests updated)
2. Locate and remove dangling ocurrences of ServiceConfig (it was renamed to Service). (No lingering references found — only TODO mention removed)
3. Update the Lwx.Builders.MicroService to provide a /health and /ready endpoints. The health endpoint must wait the Lifecycle start event before returning (use a TaskCompletionSource) to ensure the first answer will only come after the server started. (DONE - generator emits /health (waits for ApplicationStarted) and /ready endpoints in Service.ConfigureHealthz)
4. Make Lwx.Builders.MicroService initialization to list all endpoints, workers and other resources on the console. The Endpoints should be listed as method path?query -> Endpoint class. The workers should be listed as Worker class nThreads=x (DONE - generator will print a list during ApplicationStarted with endpoint/worker info)
5. Fix attribute namespace typo. (DONE - namespace `Lwx.Builders.MicroService.Atributes` renamed to `Lwx.Builders.MicroService.Atributtes`, and all references updated in attributes, processors, tests and sample projects.)