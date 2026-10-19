using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ApiFoundation.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'1000', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoSVL = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    TipoPedido = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Links",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoId = table.Column<int>(type: "integer", nullable: false),
                    Categoria = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Links_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notificacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tentativas = table.Column<int>(type: "integer", nullable: false),
                    UltimoErro = table.Column<string>(type: "text", nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificacoes_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Pedidos",
                columns: new[] { "Id", "DataCriacao", "PedidoSVL", "ServiceId", "Status", "TipoPedido" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), 1007, 21763653L, "EM_PRODUCAO", "ZLPL" },
                    { 2, new DateTime(2026, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), 1008, 21763654L, "EM_PRODUCAO", "ZCVS" },
                    { 3, new DateTime(2026, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), 1012, 21763699L, "EM_PRODUCAO", "ZMOB" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Links_PedidoId_Url",
                table: "Links",
                columns: new[] { "PedidoId", "Url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_PedidoId",
                table: "Notificacoes",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_Status",
                table: "Notificacoes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_PedidoSVL",
                table: "Pedidos",
                column: "PedidoSVL",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Links");

            migrationBuilder.DropTable(
                name: "Notificacoes");

            migrationBuilder.DropTable(
                name: "Pedidos");
        }
    }
}
