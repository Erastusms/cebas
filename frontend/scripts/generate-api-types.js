/**
 * CEBAS OpenAPI -> TypeScript Type Generator Script
 * Fetches OpenAPI v1 specification from the running backend and synchronizes types.
 */

const http = require("http");
const fs = require("fs");
const path = require("path");

const SWAGGER_URL = process.env.SWAGGER_URL || "http://localhost:5000/swagger/v1/swagger.json";
const OUTPUT_PATH = path.resolve(__dirname, "../types/generated-api-schema.json");

console.log(`[CEBAS TypeGen] Fetching OpenAPI specification from ${SWAGGER_URL}...`);

http.get(SWAGGER_URL, (res) => {
  if (res.statusCode !== 200) {
    console.error(`[CEBAS TypeGen] Failed to fetch OpenAPI spec (Status: ${res.statusCode}). Ensure backend is running.`);
    process.exit(1);
  }

  let data = "";
  res.on("data", (chunk) => (data += chunk));
  res.on("end", () => {
    try {
      const parsed = JSON.parse(data);
      fs.writeFileSync(OUTPUT_PATH, JSON.stringify(parsed, null, 2), "utf8");
      console.log(`[CEBAS TypeGen] Successfully saved OpenAPI contract to ${OUTPUT_PATH}`);
      console.log(`[CEBAS TypeGen] Next Step: Run 'npx openapi-typescript ${OUTPUT_PATH} -o types/api-schema.d.ts'`);
    } catch (err) {
      console.error("[CEBAS TypeGen] Failed to parse Swagger JSON:", err.message);
      process.exit(1);
    }
  });
}).on("error", (err) => {
  console.warn(`[CEBAS TypeGen] Backend unreachable at ${SWAGGER_URL} (${err.message}). Skipping sync for now.`);
});
