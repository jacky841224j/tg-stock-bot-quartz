using Quartz;
using TG_Stock_Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<QuartzOptions>(options =>
{
    options.Scheduling.IgnoreDuplicates = true; // default: false
    options.Scheduling.OverWriteExistingData = true; // default: true

});
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();

    // these are the defaults
    q.UseSimpleTypeLoader();
    q.UseInMemoryStore();
    q.UseDefaultThreadPool(tp =>
    {
        tp.MaxConcurrency = 10;
    });
    var jobkey = new JobKey("tg_message");
    q.AddJob<tg_message>(j => j
        .WithIdentity(jobkey.Name)
    );
    q.AddTrigger(t => t
        .ForJob(jobkey.Name)
        .WithIdentity($"{jobkey.Name}.Trigger")
        .WithCronSchedule("0/30 * * * * ? *")
        );

});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);


var app = builder.Build();
app.MapGet("/", () => $"Hello !");

app.Run();
