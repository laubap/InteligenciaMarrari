using IAApi.Services.AnomalyDetection.Multivariate;

namespace IAApi.Benchmark.Tests;

public sealed class MultivariateBufferTester
{
    private readonly MultivariateGroupBufferService _bufferService;

    public MultivariateBufferTester(
        MultivariateGroupBufferService bufferService)
    {
        _bufferService = bufferService;
    }

    public async Task ExecutarAsync()
    {
        const string clienteId = "BENCHMARK_STRESS_V1";

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("TESTE DO BUFFER MULTIVARIADO");
        Console.WriteLine("========================================");

        // Minuto 100: grupo BOILER completo.
        await _bufferService.AdicionarLeituraAsync(
            clienteId,
            "BOILER.FUEL_FLOW",
            100,
            12.5);

        await _bufferService.AdicionarLeituraAsync(
            clienteId,
            "BOILER.STEAM_FLOW",
            100,
            9.8);

        await _bufferService.AdicionarLeituraAsync(
            clienteId,
            "BOILER.STACK_TEMP",
            100,
            185.2);

        await _bufferService.AdicionarLeituraAsync(
            clienteId,
            "BOILER.O2",
            100,
            4.1);

        /*
         * Ao adicionar uma leitura do minuto 101,
         * o buffer do minuto 100 será finalizado.
         */
        var finalizados =
            await _bufferService.AdicionarLeituraAsync(
                clienteId,
                "BOILER.FUEL_FLOW",
                101,
                12.7);

        Console.WriteLine(
            $"Grupos finalizados: {finalizados.Count}");

        foreach (var grupo in finalizados)
        {
            Console.WriteLine();
            Console.WriteLine($"Cliente: {grupo.ClienteId}");
            Console.WriteLine($"Grupo: {grupo.GroupName}");
            Console.WriteLine($"Minuto: {grupo.MinuteIndex}");
            Console.WriteLine(
                $"Tags disponíveis: {grupo.Valores.Count}");
            Console.WriteLine(
                $"Possui mínimo: {grupo.PossuiMinimoDeTags()}");
            Console.WriteLine(
                $"Grupo completo: {grupo.EstaCompleto()}");

            foreach (var valor in grupo.Valores)
            {
                Console.WriteLine(
                    $"  - {valor.Key} = {valor.Value}");
            }
        }

        var pendentes =
            _bufferService.FinalizarPendentes(clienteId);

        Console.WriteLine();
        Console.WriteLine(
            $"Grupos pendentes finalizados: {pendentes.Count}");

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("TESTE DO BUFFER CONCLUÍDO");
        Console.WriteLine("========================================");
    }
}