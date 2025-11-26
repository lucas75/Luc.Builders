# Lwx.Builders — An experimental source generator based web framework

This project contains a set of source generators to make web applications.

## [Lwx.Builders.Dto](./Lwx.Builders.Dto)

A source generator to create Data Transfer Object (DTO) types with minimal boilerplate. 

The key features are the ability to use properties backed in fields or dictionary.

The dictionary variant is important for sparse tables. 

## [Lwx.Builders.Microservice](./Lwx.Builders.Microservice)

A source generator to create microservice HTTP 
API clients and servers from annotated interfaces.

# MOTIVATION 

When working with microservices, manage the boilerplate 
code becomes tedious and error-prone. The idea of those 
builders is to hide the boilerplate so the project 
becomes easier to maintain, test and code review.