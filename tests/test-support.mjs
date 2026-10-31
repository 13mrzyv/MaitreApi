import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { once } from 'node:events';
import { fileURLToPath } from 'node:url';

export async function startTestApi({ environment = 'Development', failDatabaseQueries = false } = {}) {
    const connection = process.env.ConnectionStrings__DefaultConnection;
    assert(connection, 'Set ConnectionStrings__DefaultConnection to a disposable test database.');
    const fields = Object.fromEntries(connection.split(';').filter(Boolean).map(pair => {
        const separator = pair.indexOf('=');
        return [pair.slice(0, separator).trim().toLowerCase(), pair.slice(separator + 1).trim()];
    }));
    const database = fields.database;
    assert(/^[a-z0-9_]+_test$/.test(database ?? ''), 'Use a disposable database ending in _test.');
    const psqlUrl = process.env.PGTEST_URL;
    assert(psqlUrl, 'Set PGTEST_URL to the same test database as the API connection.');
    const target = new URL(psqlUrl);
    const normalizeHost = host => host.toLowerCase() === 'localhost' ? '127.0.0.1' : host.toLowerCase();
    assert(['postgres:', 'postgresql:'].includes(target.protocol), 'Use a PostgreSQL URL.');
    assert.equal(decodeURIComponent(target.pathname.slice(1)), database, 'Test database names differ.');
    assert.equal(normalizeHost(target.hostname), normalizeHost(fields.host ?? 'localhost'), 'Test hosts differ.');
    assert.equal(target.port || '5432', fields.port ?? '5432', 'Test ports differ.');
    assert.equal(decodeURIComponent(target.username), fields.username ?? fields['user id'], 'Test users differ.');

    const sql = query => execFileSync('psql', ['-X', '-v', 'ON_ERROR_STOP=1', '-At', '-d', psqlUrl,
        '-c', query], { encoding: 'utf8', timeout: 15000 }).trim();
    assert.equal(sql('SELECT current_database();'), database);
    const assertEmpty = () => assert.equal(sql('SELECT (SELECT count(*) FROM "Products") + ' +
        '(SELECT count(*) FROM "Orders") + (SELECT count(*) FROM "OrderItems");'), '0',
        'Test database must have no application data. Do not run suites concurrently.');
    assertEmpty();
    const products = new Set();
    const orders = new Set();
    const context = { sql, psqlUrl, logs: '', base: '', products, orders };
    const cwd = fileURLToPath(new URL('../src/Maitre.Api/', import.meta.url));
    const api = spawn('dotnet', [process.env.MAITRE_API_DLL ?? 'bin/Debug/net10.0/Maitre.Api.dll',
        '--urls', 'http://127.0.0.1:0'], {
        cwd, env: { ...process.env, ASPNETCORE_ENVIRONMENT: environment,
            ConnectionStrings__DefaultConnection: connection + (failDatabaseQueries ? ';Search Path=maitre_missing_schema' : '') },
        windowsHide: true
    });
    context.api = api;
    let spawnError;
    api.on('error', error => { spawnError = error; });
    api.stdout.on('data', value => {
        context.logs += value;
        context.base = /Now listening on: (http:\/\/127\.0\.0\.1:\d+)/.exec(context.logs)?.[1] ?? '';
    });
    api.stderr.on('data', value => context.logs += value);
    context.stop = async () => {
        if (api.exitCode === null && api.signalCode === null && api.pid) {
            const exited = once(api, 'exit');
            api.kill();
            await exited;
        }
    };
    try {
        for (let attempt = 0; attempt < 200 && !context.base; attempt++) {
            if (spawnError) throw spawnError;
            if (api.exitCode !== null) throw new Error('Test API exited before startup: ' + context.logs);
            await new Promise(resolve => setTimeout(resolve, 100));
        }
        assert(context.base, 'Test API did not start: ' + context.logs);
        assert.equal((await fetch(context.base + '/health', { signal: AbortSignal.timeout(10000) })).status, 200);
    } catch (error) {
        await context.stop();
        throw error;
    }

    context.fetch = async (path, options = {}) => {
        const response = await fetch(context.base + path, { ...options, signal: AbortSignal.timeout(15000) });
        if (response.status === 201) {
            const result = await response.clone().json();
            assert(Number.isInteger(result.id) && result.id > 0);
            if (/\/products\/?$/.test(path)) products.add(result.id);
            if (/\/orders\/?$/.test(path)) orders.add(result.id);
        }
        return response;
    };
    context.cleanup = () => {
        if (orders.size) sql('DELETE FROM "OrderItems" WHERE "OrderId" IN (' + [...orders].join(',') +
            '); DELETE FROM "Orders" WHERE "Id" IN (' + [...orders].join(',') + ');');
        if (products.size) sql('DELETE FROM "Products" WHERE "Id" IN (' + [...products].join(',') + ');');
        assertEmpty();
    };
    return context;
}
