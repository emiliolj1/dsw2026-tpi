using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2026Tpi.Tests.Integration;

public sealed class CatalogContractTests
{
    [Theory]
    [InlineData("/api/doctors")]
    [InlineData("/api/specialties")]
    public async Task GetWithoutPagination_UsesDefaults(string endpoint)
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory, Roles.Patient);

        using var response = await client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJson(response);

        Assert.Equal(10, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, body.GetProperty("pageIndex").GetInt32());
        Assert.Equal(0, body.GetProperty("total").GetInt32());
        Assert.Equal(0, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task CatalogFlow_UsesLiteralJsonAndPlainTextDeletes()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory, Roles.Administrator);

        using var specialtyContent = JsonContent("""
            {
                "name": "Cardiologia",
                "description": "Especialidad para pruebas HTTP"
            }
            """);

        using var specialtyResponse = await client.PostAsync(
            "/api/specialties",
            specialtyContent);

        Assert.Equal(
            HttpStatusCode.Created,
            specialtyResponse.StatusCode);

        var specialty = await ReadJson(specialtyResponse);
        var specialtyId = specialty.GetProperty("id").GetGuid();

        Assert.NotEqual(Guid.Empty, specialtyId);
        Assert.Equal(
            "Cardiologia",
            specialty.GetProperty("name").GetString());

        using var doctorContent = JsonContent($$"""
            {
                "name": "Ana Perez",
                "licenseNumber": "MP-HTTP-001",
                "specialtyId": "{{specialtyId}}"
            }
            """);

        using var doctorResponse = await client.PostAsync(
            "/api/doctors",
            doctorContent);

        Assert.Equal(
            HttpStatusCode.Created,
            doctorResponse.StatusCode);

        var doctor = await ReadJson(doctorResponse);
        var doctorId = doctor.GetProperty("id").GetGuid();

        Assert.NotEqual(Guid.Empty, doctorId);
        Assert.Equal(
            "Ana Perez",
            doctor.GetProperty("name").GetString());
        Assert.Equal(
            "MP-HTTP-001",
            doctor.GetProperty("licenseNumber").GetString());
        Assert.Equal(
            specialtyId,
            doctor.GetProperty("specialty").GetProperty("id").GetGuid());
        Assert.Equal(
            "Cardiologia",
            doctor.GetProperty("specialty").GetProperty("name").GetString());

        using var updateContent = JsonContent($$"""
            {
                "name": "Ana Perez Actualizada",
                "licenseNumber": "MP-HTTP-002",
                "specialtyId": "{{specialtyId}}"
            }
            """);

        using var updateResponse = await client.PutAsync(
            $"/api/doctors/{doctorId}",
            updateContent);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await ReadJson(updateResponse);

        Assert.Equal(doctorId, updated.GetProperty("id").GetGuid());
        Assert.Equal(
            "Ana Perez Actualizada",
            updated.GetProperty("name").GetString());
        Assert.Equal(
            "MP-HTTP-002",
            updated.GetProperty("licenseNumber").GetString());
        Assert.Equal(
            specialtyId,
            updated.GetProperty("specialty").GetProperty("id").GetGuid());

        using var specialtyUpdateContent = JsonContent("""
            {
                "name": "Cardiologia Actualizada",
                "description": "Descripcion actualizada para pruebas"
            }
            """);

        using var specialtyUpdateResponse = await client.PutAsync(
            $"/api/specialties/{specialtyId}",
            specialtyUpdateContent);

        Assert.Equal(
            HttpStatusCode.OK,
            specialtyUpdateResponse.StatusCode);

        var updatedSpecialty = await ReadJson(specialtyUpdateResponse);

        Assert.Equal(
            specialtyId,
            updatedSpecialty.GetProperty("id").GetGuid());
        Assert.Equal(
            "Cardiologia Actualizada",
            updatedSpecialty.GetProperty("name").GetString());
        Assert.Equal(
            "Descripcion actualizada para pruebas",
            updatedSpecialty.GetProperty("description").GetString());

        using var deleteDoctorResponse = await client.DeleteAsync(
            $"/api/doctors/{doctorId}");

        await AssertPlainTextDelete(deleteDoctorResponse);

        using var deleteSpecialtyResponse = await client.DeleteAsync(
            $"/api/specialties/{specialtyId}");

        await AssertPlainTextDelete(deleteSpecialtyResponse);

        foreach (var endpoint in new[]
                 {
                     "/api/doctors",
                     "/api/specialties"
                 })
        {
            using var response = await client.GetAsync(endpoint);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await ReadJson(response);

            Assert.Equal(0, page.GetProperty("total").GetInt32());
            Assert.Equal(0, page.GetProperty("data").GetArrayLength());
        }
    }

    [Theory]
    [InlineData(null, 12)]
    [InlineData("Ana", 8)]
    public async Task DoctorFilter_ReturnsCompletePagesWithoutDuplicates(
    string? name,
    int expectedTotal)
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory, Roles.Patient);

        var targetSpeciality = new Speciality(
            "Cardiologia",
            "Especialidad buscada");

        var otherSpeciality = new Speciality(
            "Neurologia",
            "Otra especialidad");

        var targetDoctors = Enumerable.Range(1, 12)
            .Select(index => new Doctor(
                index <= 8 ? "Ana Repetida" : "Bruno Repetido",
                $"MP-TARGET-{index}",
                targetSpeciality))
            .ToArray();

        var otherDoctors = Enumerable.Range(1, 6)
            .Select(index => new Doctor(
                "Ana Repetida",
                $"MP-OTHER-{index}",
                otherSpeciality))
            .ToArray();

        var inactive = new Doctor(
            "Ana Inactiva", "MP-INACTIVE", targetSpeciality);
        inactive.Deactivate();

        var deleted = new Doctor(
            "Ana Eliminada", "MP-DELETED", targetSpeciality)
        {
            Deleted = true
        };

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<Dsw2026TpiDbContext>();

            context.AddRange(targetDoctors);
            context.AddRange(otherDoctors);
            context.AddRange(inactive, deleted);

            await context.SaveChangesAsync();
        }

        var expectedIds = targetDoctors
            .Where(doctor => name is null || doctor.Name.Contains(name))
            .Select(doctor => doctor.Id)
            .ToHashSet();

        Assert.Equal(expectedTotal, expectedIds.Count);

        const int pageSize = 3;
        var pageCount = (expectedTotal + pageSize - 1) / pageSize;
        var returnedIds = new List<Guid>();

        // Incluye una página posterior al último resultado.
        for (var pageIndex = 0; pageIndex <= pageCount; pageIndex++)
        {
            var url = "/api/doctors" +
                $"?specialtyId={targetSpeciality.Id}" +
                $"&pageSize={pageSize}&pageIndex={pageIndex}";

            if (name is not null)
            {
                url += $"&name={Uri.EscapeDataString(name)}";
            }

            using var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await ReadJson(response);

            Assert.Equal(expectedTotal, page.GetProperty("total").GetInt32());
            Assert.Equal(pageSize, page.GetProperty("pageSize").GetInt32());
            Assert.Equal(pageIndex, page.GetProperty("pageIndex").GetInt32());

            var data = page.GetProperty("data");

            var expectedPageSize = Math.Min(
                pageSize,
                Math.Max(0, expectedTotal - pageIndex * pageSize));

            Assert.Equal(expectedPageSize, data.GetArrayLength());

            foreach (var doctor in data.EnumerateArray())
            {
                Assert.Equal(
                    targetSpeciality.Id,
                    doctor.GetProperty("specialty").GetProperty("id").GetGuid());

                if (name is not null)
                {
                    Assert.Contains(
                        name,
                        doctor.GetProperty("name").GetString()!);
                }

                returnedIds.Add(doctor.GetProperty("id").GetGuid());
            }
        }

        Assert.Equal(expectedTotal, returnedIds.Count);
        Assert.Equal(expectedTotal, returnedIds.Distinct().Count());
        Assert.True(expectedIds.SetEquals(returnedIds));
    }

    [Theory]
    [InlineData(true, HttpStatusCode.BadRequest)]
    [InlineData(false, HttpStatusCode.NotFound)]
    public async Task CreateDoctor_WithInvalidSpecialty_ReturnsExpectedError(
        bool emptyId,
        HttpStatusCode expectedStatus)
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory, Roles.Administrator);

        var specialtyId = emptyId ? Guid.Empty : Guid.NewGuid();

        using var content = JsonContent($$"""
        {
            "name": "Ana Perez",
            "licenseNumber": "MP-INVALID",
            "specialtyId": "{{specialtyId}}"
        }
        """);

        using var response = await client.PostAsync(
            "/api/doctors",
            content);

        Assert.Equal(expectedStatus, response.StatusCode);

        using var listResponse = await client.GetAsync("/api/doctors");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var page = await ReadJson(listResponse);

        Assert.Equal(0, page.GetProperty("total").GetInt32());
        Assert.Equal(0, page.GetProperty("data").GetArrayLength());
    }

    private static HttpClient CreateClient(
        CustomWebApplicationFactory factory,
        string role)
    {
        var client = factory.CreateClient();

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    CustomWebApplicationFactory.JwtKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: CustomWebApplicationFactory.JwtIssuer,
            audience: CustomWebApplicationFactory.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                new JwtSecurityTokenHandler().WriteToken(token));

        return client;
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(
            json,
            Encoding.UTF8,
            "application/json");
    }

    private static async Task<JsonElement> ReadJson(
        HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task AssertPlainTextDelete(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/plain",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }
}
