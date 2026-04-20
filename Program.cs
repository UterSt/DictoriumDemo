using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DictoriumDemo.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<DictoriumDemo.App>("#app");

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Core data service (static info about all structures)
builder.Services.AddSingleton<DataStructureService>();

// Dictorium WASM bridge — scoped because IJSRuntime is scoped
builder.Services.AddScoped<DictoriumJsService>();

await builder.Build().RunAsync();
