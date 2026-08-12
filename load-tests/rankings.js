import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.BASE_URL ?? 'http://api:8080';
const vus = Number(__ENV.VUS ?? '100');
const duration = __ENV.DURATION ?? '1m';

export const options = {
  stages: [
    { duration: '30s', target: vus },
    { duration, target: vus },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
    checks: ['rate>0.99'],
  },
};

export default function () {
  const response = http.get(`${baseUrl}/rankings/players?top=10`);

  check(response, {
    'HTTPステータスが200': (result) => result.status === 200,
    // ランキングAPI以外を誤って指定しても成功扱いにならないように確認する。
    'rankings配列を含む': (result) => {
      try {
        return Array.isArray(result.json('rankings'));
      } catch {
        return false;
      }
    },
  });

  // 実ユーザーの操作間隔に近づけ、無制限送信による過度な負荷を避ける。
  sleep(1);
}
