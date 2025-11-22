# ARCHITECTURE

    Solution: Luc.Util.Web
    | Project: Lwx.Archetipe.MicroService
    | | - is a incremental source generator
    | | - generates boilerplate code for microservice projects
    | | 
    | Project: ExampleCompany.ExampleProduct.Archetype.MicroService
    | | - is a incremental source generator
    | | - customizes Lwx.Archetipe.MicroService for ExampleCompany.ExampleProduct
    | | 
    | Project: ExampleCompany.ExampleProduct.Services.Worker001
    | | - is a web project
    | | - uses all source generators above    
    | | 
    | Project: ExampleCompany.ExampleProduct.Services.Worker001.Tests
    | | - is a test project

# DEFINITIONS

- MicroService: 
  - uses minimal apis
  - generate source code for /healtz and /ready endpoints
  - generate source code for swagger /openapi endpoint
  - generate source code for authentication and authorization

- /healtz endpoint:
  - returns 200 OK if the service is running
  - install a lifecycle event to set when the server is running effectively
  - block the /healtz endpoint until the server is effectively running

- /ready endpoint:
  - returns 200 OK if the service is ready to accept requests
  - install a lifecycle event to set when the server is ready
  - block the /ready endpoint until the server is ready

- Authorization policies for endpoints
  - public public for real
  - public-on-dev public on development environment, otherwise access denied
  - internal - see bellow.

- classes anotated with [LwxEndpoint] 
  - must be in the Endpoints namespace under the root namespace of the project.
  - must declare Uri="GET /path" or similar in the attribute parameters.
  - must declare attributes necesary for swagger processing
  - must implement an Execute method with proper signature
  - must require declaration of authorization policy
  
- classes annotated with [LwxWorker] 
  - must be in the Workers namespace under the root namespace of the project.
  
- classes annotated with [LwxServiceBusConsumer] 
  - must be in the Consumer namespace under the root namespace of the project.
  - must implement an Receive method with proper signature
  - must check if the consuming project declared the dependencies correctly
  
- classes annotated with [LwxEventHubConsumer] 
  - must be in the Consumer namespace under the root namespace of the project.
  - must implement an Receive method with proper signature
  - must check if the consuming project declared the dependencies correctly

- classes annotated with [LwxTimerWorker] 
  - must be in the Workers namespace under the root namespace of the project.
  - must implement an Execute method with proper signature
  - must check if the consuming project declared the dependencies correctly

- classes annotated with [LwxServiceBusProducer] 
  - must be in the Producer namespace under the root namespace of the project.
  - must implement a Send method with proper signature
  - must check if the consuming project declared the dependencies correctly

- classes annotated with [LwxEventHubProducer] 
  - must be in the Producer namespace under the root namespace of the project.
  - must implement a Send method with proper signature
  - must check if the consuming project declared the dependencies correctly


# Internal Authentication

It is meant to be used for inter-service communication within the same organization:
- Both services must have access to the same key in the Key Vault
- The key must be a symmetric key
- The calling service calls /internal-auth/start 
- The receiving responds with a challenge 
- The calling service calls /internal-auth/complete with the encrypted challenge
- The receiving service validates and issues a session-id {UUIDv4}
- Both services keep the session-id in memory for subsequent calls

